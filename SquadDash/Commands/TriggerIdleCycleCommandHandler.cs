namespace SquadDash.Commands;

internal sealed class TriggerIdleCycleCommandHandler : IHostCommandHandler {
    private readonly Action _triggerIdle;

    public TriggerIdleCycleCommandHandler(Action triggerIdle) => _triggerIdle = triggerIdle;

    public string CommandName => "trigger_idle_cycle";

    public HostCommandResult Execute(IReadOnlyDictionary<string, string> parameters) {
        _triggerIdle();
        return new HostCommandResult(true);
    }
}
