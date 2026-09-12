using DesignPatterns.Behavioral;
using DesignPatterns.Creational;
using DesignPatterns.Structural;

namespace DesignPatterns.TeachingProjects.Tests;

public sealed class PatternBehaviorTests
{
    [Fact]
    public void Observer_UnsubscribedListenerStopsReceivingChangesWhileOtherListenersContinue()
    {
        var order = new ObserverDemo.Order("TEST-1");
        var emailEvents = new List<ObserverDemo.OrderStatusChangedEventArgs>();
        var auditEvents = new List<ObserverDemo.OrderStatusChangedEventArgs>();
        EventHandler<ObserverDemo.OrderStatusChangedEventArgs> email = (_, change) => emailEvents.Add(change);
        order.StatusChanged += email;
        order.StatusChanged += (sender, change) =>
        {
            Assert.Same(order, sender);
            Assert.Equal(change.CurrentStatus, order.Status);
            auditEvents.Add(change);
        };

        order.ChangeStatus(ObserverDemo.OrderStatus.Paid);
        order.StatusChanged -= email;
        order.ChangeStatus(ObserverDemo.OrderStatus.Shipped);

        Assert.Single(emailEvents);
        Assert.Equal(ObserverDemo.OrderStatus.Paid, emailEvents[0].CurrentStatus);
        Assert.Collection(auditEvents,
            change =>
            {
                Assert.Equal("TEST-1", change.OrderNumber);
                Assert.Equal(ObserverDemo.OrderStatus.Created, change.PreviousStatus);
                Assert.Equal(ObserverDemo.OrderStatus.Paid, change.CurrentStatus);
            },
            change =>
            {
                Assert.Equal(ObserverDemo.OrderStatus.Paid, change.PreviousStatus);
                Assert.Equal(ObserverDemo.OrderStatus.Shipped, change.CurrentStatus);
            });
    }

    [Fact]
    public void Observer_DemoStopsEmailAfterDetachButStillAuditsDelivery()
    {
        var output = new ObserverDemo().Run();

        Assert.Equal(2, output.Count(line => line.StartsWith("Email:", StringComparison.Ordinal)));
        Assert.DoesNotContain(output, line => line.StartsWith("Email:", StringComparison.Ordinal) && line.Contains("Delivered", StringComparison.Ordinal));
        Assert.Contains("Audit: ORD-42 changed Shipped -> Delivered.", output);
    }

    [Fact]
    public void Command_UndoAndRedoRestoreDocumentThroughMultipleEdits()
    {
        var document = new CommandDemo.TextDocument();
        var history = new CommandDemo.CommandHistory(new List<string>());
        history.Execute(new CommandDemo.AppendTextCommand(document, "Design"));
        history.Execute(new CommandDemo.AppendTextCommand(document, " Patterns"));
        history.Execute(new CommandDemo.ReplaceTextCommand(document, "Design", "GoF Design"));
        Assert.Equal("GoF Design Patterns", document.Text);

        history.Undo();
        Assert.Equal("Design Patterns", document.Text);
        history.Undo();
        Assert.Equal("Design", document.Text);
        history.Undo();
        Assert.Empty(document.Text);
        history.Redo();
        history.Redo();
        history.Redo();
        Assert.Equal("GoF Design Patterns", document.Text);
    }

    [Fact]
    public void Command_NewEditAfterUndoDiscardsTheOldRedoBranch()
    {
        var document = new CommandDemo.TextDocument();
        var history = new CommandDemo.CommandHistory(new List<string>());
        history.Execute(new CommandDemo.AppendTextCommand(document, "A"));
        history.Execute(new CommandDemo.AppendTextCommand(document, "B"));
        history.Undo();
        history.Execute(new CommandDemo.AppendTextCommand(document, "C"));

        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.Equal("AC", document.Text);
        history.Undo();
        Assert.Equal("A", document.Text);
    }

    [Fact]
    public void Command_EmptyHistoryRejectsUndoAndRedoWithoutChangingTheDocument()
    {
        var document = new CommandDemo.TextDocument { Text = "Untouched" };
        var history = new CommandDemo.CommandHistory(new List<string>());

        Assert.Throws<InvalidOperationException>(() => history.Undo());
        Assert.Throws<InvalidOperationException>(() => history.Redo());
        Assert.Equal("Untouched", document.Text);

        history.Execute(new CommandDemo.AppendTextCommand(document, "!"));
        history.Undo();
        Assert.Equal("Untouched", document.Text);
    }

    [Fact]
    public void Prototype_CloneCopiesValuesAndOwnsItsMutableCollection()
    {
        var original = new PrototypeDemo.CampaignDocument("Launch", "Original body", ["Email", "Web"]);
        var clone = original.Clone();
        Assert.NotSame(original, clone);
        Assert.Equal(original.Title, clone.Title);
        Assert.Equal(original.Body, clone.Body);
        Assert.Equal(original.Channels, clone.Channels);

        clone.Title = "Localized";
        clone.Body = "Localized body";
        clone.Channels.Remove("Email");
        clone.Channels.Add("Mobile");
        Assert.Equal("Launch", original.Title);
        Assert.Equal("Original body", original.Body);
        Assert.Equal(new[] { "Email", "Web" }, original.Channels);

        original.Channels.Clear();
        Assert.Equal(new[] { "Web", "Mobile" }, clone.Channels);
    }

    [Theory]
    [InlineData(false, "Operational warning", "[WARNING] Test message")]
    [InlineData(true, "Security incident", "[CRITICAL] Test message; investigate now")]
    public void Bridge_ExistingAlertsDeliverThroughANewChannel(bool security, string subject, string body)
    {
        var channel = new RecordingChannel();
        BridgeDemo.Alert alert = security
            ? new BridgeDemo.SecurityAlert(channel)
            : new BridgeDemo.OperationalAlert(channel);

        Assert.Equal("Accepted", alert.Send("recipient", "Test message"));
        Assert.Equal(("recipient", subject, body), Assert.Single(channel.Deliveries));
    }

    private sealed class RecordingChannel : BridgeDemo.IMessageChannel
    {
        public List<(string Recipient, string Subject, string Body)> Deliveries { get; } = [];

        public string Deliver(string recipient, string subject, string body)
        {
            Deliveries.Add((recipient, subject, body));
            return "Accepted";
        }
    }
}
