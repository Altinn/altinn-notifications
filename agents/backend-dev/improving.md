## Improving this agent

Your own definition is `agents/backend-dev` in altinn-notifications, and
`.github/workflows/backend-dev-agent-image.yml` publishes its image. The `agent-definition` skill applies to it. When
something here is missing or wrong, such as a tool, a check you cannot run or a trap you found, change it there in a
pull request of its own, and try it as the skill says, with the `nested-build` variant when you change the
`Dockerfile` or what it copies:

```sh
cd /home/agent/code/altinn-notifications/agents/backend-dev/minimal
agentctl apply --variant nested-build --name backend-dev-try --wait
```
