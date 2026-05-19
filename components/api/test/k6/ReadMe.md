# Automated tests using k6

## Install prerequisites

*We recommend running the tests through a container.*

**Podman (Preferred):**
```bash
podman pull grafana/k6
```

**Docker:**
```bash
docker pull grafana/k6
```

Further information on [installing k6 for running in Docker is available here.](https://k6.io/docs/get-started/installation/#docker)

Alternatively, it is possible to run the tests directly on your machine as well.

[General installation instructions are available here.](https://k6.io/docs/get-started/installation/)

---

## Configuring the secret source

**Never put secrets on the command line** - sensitive values should be passed to the k6 script via a secret source, as this provides full k6 redaction. In other words, secrets loaded via k6/secrets are automatically redacted from all k6 log output as `***SECRET_REDACTED***`, effectively preventing the values to leak into logs.

1. Create a `.secrets` file in the k6 folder
2. Copy contents from `.secrets.sample`
3. Assign valid values to the variables that are required in your intented environment (refer to the table below)


| Variable | Description | When is it required |
|----------|-------------|----------|
| `tokenGeneratorUserName` | Username for token generator | For running in envs ATxx |
| `tokenGeneratorUserPwd` | Password for token generator | For running in envs ATxx |
| `encodedJwk` | Base64-encoded JWK for signing maskinporten token requests. | For running in envs [tt02, prod] |
| `mpKid` | The key identifier of the JSON web key used to sign the maskinporten token request | For running in envs [tt02, prod] |
| `mpClientId` | The client-ID of the integration set up in Maskinporten | For running in envs [tt02, prod] |
| `subscriptionKey` | An APIM subscription key with access to the automated tests product | For running `orders-email.js` |


## Running tests

All tests are defined in the `src/tests` folder. At the top of each test file, an example command to run the test is provided.

> **Note: Command syntax for different shells**
> - **Bash**: Use the command as written below.
> - **PowerShell**: Replace `\` with a backtick (`` ` ``) at the end of each line.
> - **Command Prompt (cmd.exe)**: Replace `\` with `^` at the end of each line.

The command should be run from the `k6` folder:

```bash
cd components/api/test/k6
```

Run the test suite by specifying the filename.

**Podman (Preferred):**
```bash
podman compose run k6 run /src/tests/orders-email.js \
    --secret-source=file=/.secrets \
    -e altinn_env=*** \
    -e emailRecipient=*** \
    -e ninRecipient=*** \
    -e runFullTestSet=true
```

**Docker:**
```bash
docker compose run k6 run /src/tests/orders-email.js \
    --secret-source=file=/.secrets \
    -e altinn_env=*** \
    -e emailRecipient=*** \
    -e ninRecipient=*** \
    -e runFullTestSet=true
```

### Command Breakdown

1. **`podman compose run` / `docker compose run`**: Runs the test in a container.
2. **`k6 run {path to test file}`**: Points to the test file you want to run, e.g., `/src/tests/orders-email.js`.
3. **Script parameters**: Provided as environment variables for the container:
   ```bash
    --secret-source=file=/.secrets \
   -e altinn_env=***
   -e emailRecipient=***
   -e ninRecipient=***
   -e runFullTestSet=true
   ```

---

## Load tests

The same tests can be used to run load and performance tests. These can be executed as described above, but with additional parameters like `--vus` (virtual users) and `--duration` or `--iterations`. 

You can also disable the `runFullTestSet` parameter (or set it to `false`).

For example:

Run a test with 10 virtual users (VUs) for 5 minutes:

**Podman:**
```bash
podman compose run k6 run /src/tests/orders-email.js \
    --secret-source=file=/.secrets \
    -e altinn_env=*** \
    --vus=10 \
    --duration=5m
```

**Docker:**
```bash
docker compose run k6 run /src/tests/orders-email.js \
    --secret-source=file=/.secrets \
    -e altinn_env=*** \
    --vus=10 \
    --duration=5m
```

### Notes

The `orders-org-no.js` script contains a detailed list of organization numbers specifically tailored for the yt01 environment. For all other environments, the script uses the provided organization number, ensuring the test functions correctly for its intended purpose of functional validation.

---

## Running load tests from GitHub Actions

A `workflow_dispatch` action is created in GitHub to run load tests. Follow these steps:

1. Go to the [GitHub Actions](https://github.com/altinn/altinn-notifications/actions/workflows/performance-test.yml) page.
2. Select "Run workflow" and fill in the required parameters.
3. Tag the performance test with a descriptive name.

The test will be executed in a Kubernetes (k8s) cluster with the k6 operator, in a designated namespace.

---

## Load test results

Test results from GitHub Actions load test runs can be found in:

- GitHub Action run logs
- Grafana dashboards (if configured)
