---
# Full documentation: docs/features/maintenance-mode.md
# Task frequency options:
#   always        — run every maintenance session
#   daily         — run at most once per calendar day (UTC)
#   weekly        — run at most once per Monday–Sunday calendar week (UTC)
#   monthly       — run at most once per calendar month (UTC year+month)
#   after-commits — run once per new HEAD commit SHA
#   per-commit    — backward-compat alias for after-commits
# Set configured: true to enable maintenance mode.
# Set enabled: true on individual tasks to activate them.
# Global safety floor: per-task safety cannot be less safe than this value.
#   report-only < branch < direct
#
# INBOX REPORTING
# When a task produces a report (safety: report-only, or if_found: report),
# always deliver the findings to the user's Inbox panel by appending an
# INBOX_MESSAGE_JSON block at the very end of the response. Use from: "argus-weld".
# Example:
#
# INBOX_MESSAGE_JSON:
# {
#   "subject": "Maintenance Report: <Task Title>",
#   "from": "argus-weld",
#   "body": "## <Task Title>\n\n<Full findings in Markdown>",
#   "attachments": []
# }
#
# The body should contain the full structured report so the user can refer back
# to it without digging through the transcript. Keep the subject concise.
idle_timeout: 15
max_tasks_per_session: 5
safety: branch
enabled_on_idle: true
configured: false  # ← change to true to activate
tasks:
  - id: architectural-practices
    enabled: true
    frequency: monthly
    safety: report-only
    title: Architectural Practice Review
    instructions: |
      Review the codebase for larger architectural problems: poor separation of
      concerns, leaky abstractions, god objects, circular dependencies, missing
      service boundaries, wrong layer responsibilities, etc.

      {{#if if_found == "branch"}}
      Create a maintenance branch. Implement the improvements you identify.
      Document your reasoning in commit messages.
      {{/if}}
      {{#if if_found == "report"}}
      Do not change any code. Write a structured report of each finding:
      problem description, affected files/layers, and a recommended fix.
      Send the report to the user's Inbox using an INBOX_MESSAGE_JSON block
      (from: "argus-weld").
      {{/if}}
    options:
      if_found:
        type: radio
        label: If architectural issues are found
        tooltip: "Implement improvements or produce a report"
        value: report
        choices:
          - value: branch
            tooltip: Implement improvements on a maintenance branch
          - value: report
            tooltip: Write a report — do not change any code

  - id: code-smells
    enabled: true
    frequency: weekly
    safety: report-only
    title: Code Smell Cleanup
    instructions: |
      Scan the codebase for code smells: poor readability, long methods, unclear
      naming, overly complex conditionals, dead code, unnecessary abstraction,
      inefficient patterns, missing null checks, etc.

      {{#if if_found == "fix"}}
      Address smells inline on the current branch.
      {{/if}}
      {{#if if_found == "branch"}}
      Create a maintenance branch and address smells there.
      {{/if}}
      {{#if if_found == "report"}}
      Do not change any code. List each smell with file path, structural anchor,
      category, and a brief description of the issue and suggested fix. Send the
      report to the user's Inbox using an INBOX_MESSAGE_JSON block (from: "argus-weld").
      {{/if}}
    options:
      if_found:
        type: radio
        label: If code smells are found
        tooltip: "Fix inline, use a branch, or report only"
        value: report
        choices:
          - value: fix
            tooltip: Address smells inline on the current branch
          - value: branch
            tooltip: Create a maintenance branch and address smells there
          - value: report
            tooltip: List each smell — do not change any code

  - id: commit-review
    enabled: false
    frequency: per-commit
    safety: report-only
    title: Commit Quality Review
    instructions: |
      Retrieve the list of commits since the last time this task ran (use the
      stored last-run SHA if available; otherwise review the last 10 commits).
      For each commit, review the diff and note:
      - Code quality issues introduced (smells, complexity, missing tests)
      - Missing or inadequate commit message detail
      - Potential bugs or regressions
      - Positive patterns worth reinforcing

      Write a structured review report. Do not change any code. Send the report
      to the user's Inbox using an INBOX_MESSAGE_JSON block (from: "argus-weld").

  - id: docs-review
    enabled: true
    frequency: daily
    safety: report-only
    title: Documentation Review
    instructions: |
      Review the documentation in the `docs/` folder (or the repo's primary docs
      location) for the following issues:

      1. **Accuracy** — Are instructions, command examples, configuration values,
         and feature descriptions still accurate relative to the current codebase?
         Flag anything that appears outdated or incorrect.

      2. **Broken internal links** — Scan all Markdown files for links to other
         pages within the docs. Check whether each target file exists. If a page
         exists but has `published: false` (or equivalent front-matter), flag any
         other page that links to it as a warning — the reader will hit an
         unpublished page.

      3. **Broken external links** — Optionally check HTTP/HTTPS links to see if
         they return a non-200 status. Flag dead external links.

      4. **Missing images** — Find image references (`![...](...)`). Check whether
         the referenced file exists on disk. Flag missing image files.

      5. **Orphaned pages** — Identify docs pages that are not reachable from any
         other page (no inbound links from within the docs tree). These may be
         forgotten or accidentally unpublished pages.

      {{#if if_found == "report"}}
      Do not change any files. Produce a structured report grouped by issue type,
      listing file path, structural anchor (e.g. `## Section > ### Subsection`),
      and a description of each problem found. Include a severity: Warning for
      unpublished-page links and dead links, Info for orphaned pages and accuracy
      concerns. Send the report to the user's Inbox using an INBOX_MESSAGE_JSON
      block (from: "argus-weld").
      {{/if}}
      {{#if if_found == "fix"}}
      Correct accuracy issues and fix broken links where possible (e.g. update a
      link target, remove a dead link). Commit changes to a maintenance branch.
      Items that require human judgment (accuracy rewrites, missing images) should
      still be reported.
      {{/if}}
    options:
      if_found:
        type: radio
        label: If documentation issues are found
        tooltip: "Produce a report or fix what can be fixed automatically"
        value: report
        choices:
          - value: report
            tooltip: Write a report — do not change any files
          - value: fix
            tooltip: Fix auto-correctable issues on a maintenance branch; report the rest

  - id: eliminate-duplication
    enabled: false
    frequency: daily
    safety: report-only
    title: Eliminate Code Duplication
    instructions: |
      Scan the codebase for duplicated logic — identical or near-identical code
      blocks, copy-pasted utility functions, repeated patterns that should be
      extracted. Focus on meaningful duplication (not trivial one-liners).

      {{#if if_found == "fix"}}
      Refactor inline on the current branch. Extract shared logic, update all
      call sites, ensure tests still pass.
      {{/if}}
      {{#if if_found == "branch"}}
      Create a maintenance branch and refactor there.
      {{/if}}
      {{#if if_found == "report"}}
      Do not change any code. List each duplication instance with file paths,
      structural anchors, and a brief description of the shared logic. Send the
      report to the user's Inbox using an INBOX_MESSAGE_JSON block (from: "argus-weld").
      {{/if}}
    options:
      if_found:
        type: radio
        label: If duplication is found
        tooltip: "Refactor now, create a branch, or just report"
        value: report
        choices:
          - value: fix
            tooltip: Refactor inline on the current branch
          - value: branch
            tooltip: Create a maintenance branch and refactor there
          - value: report
            tooltip: List each instance — do not change any code

  - id: error-handling-audit
    enabled: true
    frequency: weekly
    safety: report-only
    title: Error Handling Audit
    instructions: |
      Audit the codebase for error-handling gaps and unsafe exception patterns.
      Examine every area listed below. For each finding, record: file path,
      structural anchor (class + method), category (from the list), severity
      (Critical / High / Medium / Low), and a concrete description of the problem
      and the recommended fix.

      **Categories to check:**

      1. **Silent catch blocks** — `catch { }` or `catch (Exception)` with an empty
         body, a comment only, or a `/* best-effort */` marker that suppresses a
         genuinely important failure. Distinguish truly best-effort (e.g. UI cleanup
         on shutdown) from silently swallowed errors that users or callers need to
         know about.

      2. **Catch-and-return-null / false** — methods that catch internally and return
         a sentinel value (null, false, -1) with no out-param, no log, and no way for
         the caller to distinguish "not found" from "threw unexpectedly". Callers
         silently proceed as if nothing happened.

      3. **WPF event handlers without try-catch** — Button.Click, Loaded, Timer.Tick,
         DispatcherTimer.Tick, and similar UI callbacks. Unhandled exceptions here
         reach `App_DispatcherUnhandledException` but only if the dispatcher is still
         running; partial failures may leave UI in inconsistent state. Each handler
         should catch, log, and surface or recover gracefully.

      4. **`async void` methods** — any `async void` that is not a WPF/legacy event
         handler is dangerous: exceptions escape all await chains and cannot be
         caught by callers. Flag non-event-handler `async void` methods and evaluate
         whether they should be `async Task`. Also flag `async void` event handlers
         that have no internal try-catch, since exceptions in those escape to the
         `DispatcherUnhandledException` handler with no contextual recovery.

      5. **Fire-and-forget tasks** — `Task.Run(...)`, `_ = SomeAsync()`, or
         `SomeAsync().ConfigureAwait(false)` that discard the task without attaching
         a `.ContinueWith` error handler or `await`-ing inside a safe context. Lost
         task exceptions are only surfaced via `TaskScheduler.UnobservedTaskException`
         which may fire too late to be useful.

      6. **Missing `finally` / `using` for resource cleanup** — code paths that
         acquire a lock, open a file, start an operation, or subscribe an event
         handler inside a try block but do not guarantee cleanup in a `finally` or
         via `using`/`IDisposable`. An exception partway through leaves resources
         leaked or handlers registered forever.

      7. **Overly broad exception catches** — `catch (Exception)` used where a
         narrower type (e.g. `IOException`, `JsonException`, `OperationCanceledException`)
         would be more appropriate. Broad catches can mask programming errors (e.g.
         `NullReferenceException`, `ArgumentException`) that should propagate and be
         fixed rather than swallowed.

      8. **Exception messages missing context** — throw/catch sites that construct
         exception messages or log entries without including the relevant context:
         operation name, file path, input value, object identity, or state at the
         time of failure. A bare `"Parsing failed"` is not actionable; include the
         source, the content, and the position if known.

      9. **Background-thread exceptions not marshaled to UI** — code running on
         `ThreadPool`, `Task.Run`, or explicit `Thread` instances that catches
         exceptions and logs them without also dispatching a user-visible notification.
         Silent background failures leave the UI in an inconsistent state with no
         indication to the user that something went wrong.

      10. **OperationCanceledException swallowed** — `OperationCanceledException`
          and `TaskCanceledException` should usually not be caught silently; they
          represent intentional cancellation and callers/awaiters need to observe
          them. Flag catch blocks that swallow these without rethrowing or
          propagating through the cancellation chain.

      {{#if if_found == "report"}}
      Do not change any code. Produce a structured report grouped by category,
      then by severity within each category. For each finding include:
      - File path and structural anchor (ClassName.MethodName or similar)
      - Severity: Critical / High / Medium / Low
      - Description of the problem
      - Recommended fix

      At the end, include a brief summary: total findings by severity, and the
      two or three highest-priority items to address first.

      Send the report to the user's Inbox using an INBOX_MESSAGE_JSON block
      (from: "argus-weld").
      {{/if}}
      {{#if if_found == "fix"}}
      Fix issues that are safe to patch automatically on a maintenance branch:
      - Add logging to silent catch blocks where the failure is non-trivial
      - Wrap bare async-void event handlers in try-catch with trace logging
      - Add `using` or `finally` for obviously leaked resources
      Issues requiring design decisions (restructuring callers, changing return
      types, async void→Task conversions with call-site changes) should be
      reported instead in an INBOX_MESSAGE_JSON block (from: "argus-weld").
      {{/if}}
    options:
      if_found:
        type: radio
        label: If error-handling gaps are found
        tooltip: "Produce a report or patch safe fixes automatically"
        value: report
        choices:
          - value: report
            tooltip: Write a structured report — do not change any code
          - value: fix
            tooltip: Patch safe fixes on a maintenance branch; report the rest

  - id: magic-numbers
    enabled: false
    frequency: daily
    safety: report-only
    title: Extract Magic Numbers and Hardcoded Strings
    instructions: |
      Scan the codebase for magic numbers (numeric literals used in logic without
      explanation) and hardcoded strings that belong in named constants or
      configuration (connection strings, URLs, thresholds, timeouts, limits, etc.).

      {{#if if_found == "extract"}}
      Extract each magic value into a named constant or config entry. Update all
      references. Commit to a maintenance branch.
      {{/if}}
      {{#if if_found == "report"}}
      Do not change any code. List each instance with file path, structural anchor
      (e.g. ClassName.MethodName), the literal value, and a suggested constant name.
      {{/if}}
    options:
      if_found:
        type: radio
        label: If magic numbers or hardcoded strings are found
        tooltip: "Extract to constants or report them"
        value: report
        choices:
          - value: extract
            tooltip: Extract to named constants; commit to a maintenance branch
          - value: report
            tooltip: List each instance — do not change any code

  - id: naming-conventions
    enabled: false
    frequency: daily
    safety: report-only
    title: Naming Convention Audit
    instructions: |
      Audit the codebase for naming inconsistencies:
      - Variables, fields, properties, methods deviating from the project's
        established convention (PascalCase, camelCase, snake_case, etc.)
      - Inconsistent pluralisation (e.g. `items` vs `itemList` vs `itemCollection`)
      - Abbreviations used in some places but not others
      - Test method naming inconsistencies

      {{#if if_found == "fix"}}
      Rename inconsistencies directly on the current branch. Update all
      references. Ensure the project still builds.
      {{/if}}
      {{#if if_found == "report"}}
      Do not change any code. List each inconsistency with file path, structural
      anchor (e.g. ClassName.MethodName), current name, and suggested name.
      Send the report to the user's Inbox using an INBOX_MESSAGE_JSON block
      (from: "argus-weld").
      {{/if}}
    options:
      if_found:
        type: radio
        label: If naming inconsistencies are found
        tooltip: "Rename inconsistencies or produce a report"
        value: report
        choices:
          - value: fix
            tooltip: Rename directly on the current branch; update all references
          - value: report
            tooltip: List each inconsistency — do not change any code

  - id: speed-improvements
    enabled: false
    frequency: daily
    safety: report-only
    title: Performance Improvements
    instructions: |
      Review the codebase for performance opportunities: inefficient algorithms,
      unnecessary allocations, repeated expensive operations, missing caching,
      synchronous I/O where async would improve throughput, LINQ queries that
      could be rewritten, N+1 query patterns, etc.

      {{#if if_found == "fix"}}
      Implement optimisations inline on the current branch. Add a brief comment
      explaining the change where the improvement is non-obvious.
      {{/if}}
      {{#if if_found == "branch"}}
      Create a maintenance branch and implement improvements there.
      {{/if}}
      {{#if if_found == "report"}}
      Do not change any code. Describe each opportunity, its likely impact, and
      the recommended approach. Send the report to the user's Inbox using an
      INBOX_MESSAGE_JSON block (from: "argus-weld").
      {{/if}}
    options:
      if_found:
        type: radio
        label: If performance opportunities are found
        tooltip: "Implement optimisations or report opportunities"
        value: report
        choices:
          - value: fix
            tooltip: Implement optimisations inline on the current branch
          - value: branch
            tooltip: Create a maintenance branch for the optimisations
          - value: report
            tooltip: Describe each opportunity — do not change any code

  - id: prune-tasks
    enabled: false
    frequency: monthly
    safety: direct
    title: Prune Completed Tasks
    instructions: |
      Open `.squad/tasks.md`. Remove all items that are marked as completed
      (`[x]`) and have no open sub-tasks. Archive removed items to
      `.squad/tasks-archive.md` (append, do not overwrite) with a timestamp.

      Do not modify any source files.

  - id: readme-currency
    enabled: false
    frequency: daily
    safety: report-only
    title: README Currency Check
    instructions: |
      Compare README.md (and any other top-level docs) against the current state
      of the codebase. Check for:
      - Setup or build instructions that no longer match the actual commands
      - Outdated dependency versions or requirements
      - References to files, directories, or features that no longer exist
      - Missing documentation for significant new features or changed APIs

      Write a gap report. Do not change any files. Send the report to the user's
      Inbox using an INBOX_MESSAGE_JSON block (from: "argus-weld").

  - id: run-tests
    enabled: false
    frequency: daily
    safety: branch
    title: Run Tests
    instructions: |
      Run all tests in the repository. Use the appropriate test runner for this
      project (e.g. `dotnet test`, `npm test`, `go test ./...`).

      {{#if if_failing == "fix"}}
      Diagnose each failing test. Fix the root cause in source — do not delete
      tests or weaken assertions. Commit all fixes to the branch.
      {{/if}}
      {{#if if_failing == "report"}}
      Do not change any code. Write a summary of every failing test, the error
      message, and your diagnosis of the likely cause. Send the report to the
      user's Inbox using an INBOX_MESSAGE_JSON block (from: "argus-weld").
      {{/if}}
    options:
      if_failing:
        type: radio
        label: If failing tests are found
        tooltip: "Fix failures or only report them"
        value: fix
        choices:
          - value: fix
            tooltip: Fix each failing test; commit fixes to the branch
          - value: report
            tooltip: Report failures only — do not change any code

  - id: security-audit
    enabled: true
    frequency: weekly
    safety: report-only
    title: Security Vulnerability Audit
    instructions: |
      Audit the codebase for security vulnerabilities and unsafe patterns.
      Focus on:
      - Injection risks (SQL, command, path traversal, format string)
      - Secrets or credentials hard-coded or logged in plain text
      - Unsafe deserialization or untrusted data passed to eval-equivalent APIs
      - Missing input validation or output encoding (XSS, open redirect)
      - Overly broad exception catches that swallow security-relevant errors
      - Insecure cryptography (MD5/SHA1 for integrity, ECB mode, short keys, hard-coded IVs)
      - Dependency or NuGet package references with known CVEs (check via `dotnet list package --vulnerable` if available)
      - File or network operations that trust caller-supplied paths without sanitisation
      - Sensitive data written to logs, temp files, or crash dumps
      {{#if if_found == "report"}}
      Do not change any code. Produce a structured report grouped by severity
      (Critical / High / Medium / Low), listing file path, structural anchor
      (e.g. ClassName.MethodName), and a description of each finding.
      Send the report to the user's Inbox using an INBOX_MESSAGE_JSON block
      (from: "argus-weld").
      {{/if}}
      {{#if if_found == "fix"}}
      Fix issues that are safe to patch automatically (remove hard-coded secrets,
      add input guards, replace deprecated crypto calls). Commit to a maintenance
      branch. Issues requiring design decisions or external dependency updates
      should still be reported in an INBOX_MESSAGE_JSON block (from: "argus-weld").
      {{/if}}
    options:
      if_found:
        type: radio
        label: If vulnerabilities are found
        tooltip: "Produce a report or patch what can be fixed automatically"
        value: report
        choices:
          - value: report
            tooltip: Write a report — do not change any files
          - value: fix
            tooltip: Auto-patch safe fixes on a maintenance branch; report the rest

  - id: todo-fixme-scan
    enabled: false
    frequency: per-commit
    safety: direct
    title: TODO / FIXME / HACK Scanner
    instructions: |
      Scan all source files for TODO, FIXME, HACK, XXX, and NOTE comments.
      For each comment found, create a task entry in `.squad/tasks.md` if one
      does not already exist for that comment. Include the file path, line number,
      and the full comment text in the task description.

      Do not modify any source files. Only append to `.squad/tasks.md`.

  - id: unused-dependencies
    enabled: false
    frequency: daily
    safety: report-only
    title: Unused Dependency Scan
    instructions: |
      Check for NuGet packages (*.csproj), npm packages (package.json), or other
      dependency manifests in the repository. Identify packages that appear to be
      unused (not referenced in source or only referenced transitively).

      Write a report listing each potentially unused dependency, the manifest file
      it appears in, and a note on how to verify and remove it. Do not change any
      files. Send the report to the user's Inbox using an INBOX_MESSAGE_JSON block
      (from: "argus-weld").

  - id: xml-doc-coverage
    enabled: false
    frequency: daily
    safety: report-only
    title: XML Doc Comment Coverage
    instructions: |
      Scan all public types, methods, properties, and interfaces in the C# source
      for missing XML doc comments (`<summary>`, `<param>`, `<returns>`).
      Produce a coverage report grouped by file, listing each undocumented member.
      Do not change any code. Send the report to the user's Inbox using an
      INBOX_MESSAGE_JSON block (from: "argus-weld").

      If this is not a C# project, adapt to the equivalent docstring convention
      (JSDoc for TypeScript/JavaScript, docstrings for Python, godoc for Go).
---
