namespace SquadDash;

/// <summary>
/// Provides the <see cref="PromptInstructionSet"/> used to compose every AI prompt.
/// The default implementation returns the built-in policy strings; a test or
/// alternative implementation can substitute them without recompiling.
/// </summary>
internal interface IPromptInstructionProvider {
    PromptInstructionSet Get();
}
