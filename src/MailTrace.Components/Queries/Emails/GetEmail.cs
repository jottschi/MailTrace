﻿namespace MailTrace.Components.Queries.Emails
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using MailTrace.Components.Helpers;
    using MailTrace.Data;

    using MediatR;

    public class GetEmail
    {
        public class Query : IRequest<Result>
        {
            public string MessageId { get; set; }
        }

        public class Result
        {
            public string MessageId { get; set; }

            public string From { get; set; }

            public string Size { get; set; }

            public string Client { get; set; }

            public string NumberOfRecipients { get; set; }

            public IList<Attempt> Attempts { get; set; }

            public IList<RecipientStatus> RecipientStatuses { get; set; }
        }

        public class Attempt
        {
            public string Host { get; set; }

            public string QueueId { get; set; }

            public string DsnCode { get; set; }

            public string Status { get; set; }

            public string Relay { get; set; }

            public string Delay { get; set; }

            public string Delays { get; set; }

            public string To { get; set; }

            public DateTime? SourceTime { get; set; }

            public string OriginallyTo { get; set; }
        }

        public class RecipientStatus
        {
            public string To { get; set; }

            public string DsnCode { get; set; }

            public string Status { get; set; }

            public DateTime? SourceTime { get; set; }
        }
    }

    public class GetEmailHandler : IRequestHandler<GetEmail.Query, GetEmail.Result>
    {
        private readonly TraceContext _context;

        public GetEmailHandler(TraceContext context)
        {
            _context = context;
        }

        public GetEmail.Result Handle(GetEmail.Query message)
        {
            var emailAttributes = (from m in _context.EmailLogs.Where(x => x.Key == "message-id" && x.Value == message.MessageId)
                                   join attr in _context.EmailLogs on new {m.QueueId, m.Host} equals new {attr.QueueId, attr.Host}
                                   where new[] {"message-id", "from", "size", "client", "nrcpt"}.Contains(attr.Key)
                                   select new
                                   {
                                       attr.Key,
                                       attr.Value
                                   })
                .Distinct()
                .ToLookup(x => x.Key)
                .ToDictionary(x => x.Key, x => x.First().Value);

            if (!emailAttributes.Any())
            {
                return null;
            }

            var result = new GetEmail.Result
            {
                Client = emailAttributes.GetOrDefault("client"),
                From = emailAttributes.GetOrDefault("from"),
                MessageId = emailAttributes.GetOrDefault("message-id"),
                Size = emailAttributes.GetOrDefault("size"),
                NumberOfRecipients = emailAttributes.GetOrDefault("nrcpt"),
            };

            var attemptAttributes = (from m in _context.EmailLogs.Where(x => x.Key == "message-id" && x.Value == message.MessageId)
                                     join attr in _context.EmailLogs on new {m.QueueId, m.Host} equals new {attr.QueueId, attr.Host}
                                     where new[] {"relay", "delay", "delays", "dsn", "status", "to", "orig_to"}.Contains(attr.Key)
                                     select new
                                     {
                                         attr.Host,
                                         attr.QueueId,
                                         attr.Key,
                                         attr.Value,
                                         attr.SourceTime,
                                         attr.LogId
                                     })
                .AsEnumerable()
                .GroupBy(x => x.LogId)
                .Select(g => g.ToLookup(x => x.Key).ToDictionary(x => x.Key, x => x.FirstOrDefault()))
                .Select(attemptDictionary => new GetEmail.Attempt
                {
                    To = attemptDictionary.GetOrDefault("to")?.Value,
                    OriginallyTo = attemptDictionary.GetOrDefault("orig_to")?.Value,
                    Relay = attemptDictionary.GetOrDefault("relay")?.Value,
                    Delay = attemptDictionary.GetOrDefault("delay")?.Value,
                    Delays = attemptDictionary.GetOrDefault("delays")?.Value,
                    DsnCode = attemptDictionary.GetOrDefault("dsn")?.Value,
                    Status = attemptDictionary.GetOrDefault("status")?.Value,
                    SourceTime = attemptDictionary.FirstOrDefault().Value?.SourceTime,
                    Host = attemptDictionary.FirstOrDefault().Value?.Host,
                    QueueId = attemptDictionary.FirstOrDefault().Value?.QueueId
                })
                .OrderByDescending(x => x.SourceTime)
                .ToList();

            result.Attempts = attemptAttributes;

            result.RecipientStatuses = (from attempt in attemptAttributes
                                        group attempt by attempt.To
                                        into g
                                        let lastAttempt = g.OrderByDescending(x => x.SourceTime).FirstOrDefault()
                                        select new GetEmail.RecipientStatus
                                        {
                                            To = lastAttempt.To,
                                            SourceTime = lastAttempt.SourceTime,
                                            DsnCode = lastAttempt.DsnCode,
                                            Status = lastAttempt.Status
                                        })
                .ToList();

            return result;
        }
    }
}