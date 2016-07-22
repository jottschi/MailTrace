﻿namespace MailTrace.Host.Tests.Queries
{
    using System;
    using System.IO;
    using System.Linq;

    using Effort.DataLoaders;

    using FluentAssertions;

    using MailTrace.Data;
    using MailTrace.Host.Queries.Emails;

    using Xunit;

    public class ListEmailsFacts
    {
        private readonly TraceContext _context;

        public ListEmailsFacts()
        {
            var loader = new CsvDataLoader(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Defered"));

            var connection = Effort.DbConnectionFactory.CreateTransient(loader);
            _context = new TraceContext(connection);
        }

        [Fact]
        public void Can_get_list_of_emails()
        {
            var query = new ListEmails.Query();
            var handler = new ListEmailsHandler(_context);

            // Act

            var result = handler.Handle(query);

            // Assert
            result.Logs.Should().HaveCount(1);

            var email = result.Logs.First();
            email.Delay.Should().Be("32423");
            email.DsnCode.Should().Be("2.0.0");
            email.From.Should().Be("<dmarc-reporter@silvenga.com>");
            email.FirstSeen.Should().Be(new DateTime(2016, 7, 19, 1, 5, 21)); // 
            email.MessageId.Should().Be("<a1468908015.96715@assp.silvenga.lan>");
            email.Size.Should().Be("2018");
            email.Status.Should().Be("sent (250 ok 1468940743 qp 15565)");
            email.To.Should().Be("<abuse@froninsa.win>");
        }
    }
}