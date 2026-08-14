namespace DesignPatterns.Structural;

/// <summary>
/// Demonstrates independent alert-type and delivery-channel hierarchies joined by composition.
/// </summary>
public sealed class BridgeDemo : IPatternDemo
{
    public string Key => "bridge";

    public string Name => "Bridge / 桥接模式";

    public string Category => "Structural";

    public string Intent => "把抽象层次与实现层次分离，使二者可以独立扩展并自由组合。";

    public IReadOnlyList<string> Run()
    {
        Alert operationalEmail = new OperationalAlert(new EmailChannel());
        Alert operationalSms = new OperationalAlert(new SmsChannel());
        Alert securityEmail = new SecurityAlert(new EmailChannel());

        return
        [
            operationalEmail.Send("ops@example.test", "CPU usage reached 92%"),
            operationalSms.Send("+81-000-0000", "CPU usage reached 92%"),
            securityEmail.Send("security@example.test", "Five failed sign-in attempts"),
            "新增告警类型无需修改渠道；新增渠道也无需修改告警类型。"
        ];
    }

    // Implementor hierarchy: delivery details vary independently from alert semantics.
    private interface IMessageChannel
    {
        string Deliver(string recipient, string subject, string body);
    }

    private sealed class EmailChannel : IMessageChannel
    {
        public string Deliver(string recipient, string subject, string body) =>
            $"EMAIL to={recipient} | subject={subject} | body={body}";
    }

    private sealed class SmsChannel : IMessageChannel
    {
        public string Deliver(string recipient, string subject, string body) =>
            $"SMS to={recipient} | {subject}: {body}";
    }

    // Abstraction hierarchy keeps a bridge (Channel) instead of inheriting delivery behavior.
    private abstract class Alert(IMessageChannel channel)
    {
        protected IMessageChannel Channel { get; } = channel;

        public abstract string Send(string recipient, string message);
    }

    private sealed class OperationalAlert(IMessageChannel channel) : Alert(channel)
    {
        public override string Send(string recipient, string message) =>
            Channel.Deliver(recipient, "Operational warning", $"[WARNING] {message}");
    }

    private sealed class SecurityAlert(IMessageChannel channel) : Alert(channel)
    {
        public override string Send(string recipient, string message) =>
            Channel.Deliver(recipient, "Security incident", $"[CRITICAL] {message}; investigate now");
    }
}
