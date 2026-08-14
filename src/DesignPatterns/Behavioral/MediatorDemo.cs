namespace DesignPatterns.Behavioral;

/// <summary>
/// Uses an air-traffic-control tower to coordinate aircraft that never talk directly.
/// </summary>
public sealed class MediatorDemo : IPatternDemo
{
    public string Key => "mediator";

    public string Name => "Mediator / 中介者模式";

    public string Category => "Behavioral";

    public string Intent => "用中介者集中协调对象交互，降低对象之间的直接耦合。";

    public IReadOnlyList<string> Run()
    {
        var output = new List<string>();
        var tower = new ControlTower(output);
        var flight101 = new Aircraft("OA101", tower);
        var flight202 = new Aircraft("JL202", tower);
        var flight303 = new Aircraft("SQ303", tower);

        flight101.RequestLanding();
        flight202.RequestLanding();
        flight303.RequestLanding();
        flight101.CompleteLanding();
        flight202.CompleteLanding();

        return output;
    }

    private interface IControlTower
    {
        void RequestLanding(Aircraft aircraft);

        void CompleteLanding(Aircraft aircraft);
    }

    // Mediator: it owns runway coordination, keeping that policy out of each aircraft.
    private sealed class ControlTower : IControlTower
    {
        private readonly Queue<Aircraft> _waiting = new();
        private readonly ICollection<string> _output;
        private Aircraft? _onRunway;

        internal ControlTower(ICollection<string> output)
        {
            _output = output;
        }

        public void RequestLanding(Aircraft aircraft)
        {
            if (_onRunway is null)
            {
                ClearToLand(aircraft);
                return;
            }

            _waiting.Enqueue(aircraft);
            _output.Add($"Tower queued {aircraft.CallSign}; runway occupied by {_onRunway.CallSign}.");
        }

        public void CompleteLanding(Aircraft aircraft)
        {
            if (!ReferenceEquals(_onRunway, aircraft))
            {
                _output.Add($"Tower ignored completion from {aircraft.CallSign}; it does not own the runway.");
                return;
            }

            _output.Add($"{aircraft.CallSign} cleared the runway.");
            _onRunway = null;

            if (_waiting.TryDequeue(out var next))
            {
                ClearToLand(next);
            }
        }

        private void ClearToLand(Aircraft aircraft)
        {
            _onRunway = aircraft;
            _output.Add($"Tower cleared {aircraft.CallSign} to land.");
        }
    }

    // Colleague: it knows only the mediator contract, not the other aircraft.
    private sealed class Aircraft
    {
        private readonly IControlTower _tower;

        internal Aircraft(string callSign, IControlTower tower)
        {
            CallSign = callSign;
            _tower = tower;
        }

        internal string CallSign { get; }

        internal void RequestLanding() => _tower.RequestLanding(this);

        internal void CompleteLanding() => _tower.CompleteLanding(this);
    }
}
