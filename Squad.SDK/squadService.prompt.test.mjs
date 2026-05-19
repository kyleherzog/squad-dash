import assert from "node:assert/strict";
import { test } from "node:test";
import {
    approvePermissionRequest,
    buildNamedAgentExecutionPrompt,
    resolvePermissionApprovalKind,
    resolvePermissionApprovalKindFromSchema,
    SquadBridgeService
} from "./squadService.js";

test("permission approval supports legacy Copilot runtime response shape", () => {
    assert.equal(resolvePermissionApprovalKind("1.0.24"), "approved");
});

test("permission approval supports current Copilot runtime response shape", () => {
    assert.equal(resolvePermissionApprovalKind("1.0.36"), "approve-once");
});

test("permission approval detects legacy Copilot schema", () => {
    const schema = JSON.stringify({
        session: {
            permissions: {
                handlePendingPermissionRequest: {
                    params: {
                        properties: {
                            result: {
                                anyOf: [
                                    { properties: { kind: { const: "approved" } } }
                                ]
                            }
                        }
                    }
                }
            }
        }
    });

    assert.equal(resolvePermissionApprovalKindFromSchema(schema), "approved");
});

test("permission approval detects current Copilot schema", () => {
    const schema = JSON.stringify({
        session: {
            permissions: {
                handlePendingPermissionRequest: {
                    params: {
                        properties: {
                            result: { $ref: "#/definitions/PermissionDecision" }
                        }
                    }
                }
            }
        },
        definitions: {
            PermissionDecision: {
                anyOf: [
                    { properties: { kind: { const: "approve-once" } } }
                ]
            }
        }
    });

    assert.equal(resolvePermissionApprovalKindFromSchema(schema), "approve-once");
});

test("permission approval returns a supported response shape", () => {
    assert.match(approvePermissionRequest().kind, /^(approved|approve-once)$/);
});

test("named-agent quick-reply prompt includes handoff in the submitted prompt", () => {
    const prompt = buildNamedAgentExecutionPrompt(
        "Sorin - implement the 3 optimizations",
        "sorin-pyre",
        [
            "SquadDash quick-reply handoff context.",
            "Source turn:",
            "1. ResolveSquadVersionAsync - cache or fire-and-forget",
            "2. OpenWorkspace - profile conversation load",
            "3. Mutex timeout on settings save during shutdown"
        ].join("\n"),
        "# Sorin Pyre\nPerformance Engineer");

    assert.match(prompt, /You are @sorin-pyre/);
    assert.match(prompt, /Visible quick reply selected by the user: "Sorin - implement the 3 optimizations"/);
    assert.match(prompt, /Treat this prompt as the complete task brief/);
    assert.match(prompt, /ResolveSquadVersionAsync - cache or fire-and-forget/);
    assert.match(prompt, /OpenWorkspace - profile conversation load/);
    assert.match(prompt, /Mutex timeout on settings save during shutdown/);
    assert.match(prompt, /# Sorin Pyre/);
});

test("background task cancellation retries paired tool call id when agent id is rejected", async () => {
    const service = new SquadBridgeService();
    const attempts = [];
    service.sessions.set("session-1", {
        session: {
            sessionId: "session-1",
            cancelBackgroundTask: async taskId => {
                attempts.push(taskId);
                return taskId === "tool-call-1";
            },
            getBackgroundTasks: async () => []
        },
        backgroundTasks: {
            agents: [
                {
                    agentId: "agent-1",
                    toolCallId: "tool-call-1"
                }
            ],
            shells: []
        }
    });

    const cancelled = await service.cancelBackgroundTask("agent-1");

    assert.equal(cancelled, true);
    assert.deepEqual(attempts, ["agent-1", "tool-call-1"]);
});

test("background task cancellation retries remembered task name for tool call id", async () => {
    const service = new SquadBridgeService();
    const attempts = [];
    service.sessions.set("session-1", {
        session: {
            sessionId: "session-1",
            cancelBackgroundTask: async taskId => {
                attempts.push(taskId);
                return taskId === "queue-held-restart-bug";
            },
            getBackgroundTasks: async () => []
        },
        backgroundTasks: {
            agents: [],
            shells: []
        },
        backgroundTaskIdsByToolCallId: new Map([
            ["toolu_bdrk_014NnSZxJapiCigA8z7DfSS3", "queue-held-restart-bug"]
        ])
    });

    const cancelled = await service.cancelBackgroundTask("toolu_bdrk_014NnSZxJapiCigA8z7DfSS3");

    assert.equal(cancelled, true);
    assert.deepEqual(attempts, [
        "toolu_bdrk_014NnSZxJapiCigA8z7DfSS3",
        "queue-held-restart-bug"
    ]);
});

test("background task cancel diagnostics include remembered mapping and snapshot matches", () => {
    const service = new SquadBridgeService();
    service.sessions.set("session-1", {
        session: {
            sessionId: "session-1"
        },
        backgroundTasks: {
            agents: [
                {
                    agentId: "queue-held-restart-bug",
                    toolCallId: "toolu_bdrk_014NnSZxJapiCigA8z7DfSS3"
                }
            ],
            shells: []
        },
        backgroundTaskIdsByToolCallId: new Map([
            ["toolu_bdrk_014NnSZxJapiCigA8z7DfSS3", "queue-held-restart-bug"]
        ])
    });

    const diagnostics = service.describeBackgroundCancelState("toolu_bdrk_014NnSZxJapiCigA8z7DfSS3");

    assert.match(diagnostics, /requested=toolu_bdrk_014NnSZxJapiCigA8z7DfSS3/);
    assert.match(diagnostics, /mappedTaskId=queue-held-restart-bug/);
    assert.match(diagnostics, /matchingAgents=queue-held-restart-bug\/toolu_bdrk_014NnSZxJapiCigA8z7DfSS3/);
});
