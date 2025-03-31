# Automated tests using k6

## Install prerequisites

*We recommend running the tests through a Docker container.*

From the command line:

```bash
docker pull grafana/k6
```

Further information on [installing k6 for running in Docker is available here.](https://k6.io/docs/get-started/installation/#docker)

Alternatively, it is possible to run the tests directly on your machine as well.

[General installation instructions are available here.](https://k6.io/docs/get-started/installation/)

---

## Running tests

All tests are defined in the `src/tests` folder. At the top of each test file, an example command to run the test is provided.

The command should be run from the `k6` folder:

```bash
$> cd /altinn-notifications/test/k6
```

Run the test suite by specifying the filename.

For example:

```bash
$> podman compose run k6 run /src/tests/orders-email.js \
    -e tokenGeneratorUserName=*** \
    -e tokenGeneratorUserPwd=*** \
    -e env=*** \
    -e emailRecipient=*** \
    -e ninRecipient=*** \
    -e runFullTestSet=true
```

### Command Breakdown

1. **`podman compose run`**: Runs the test in a Docker container.
2. **`k6 run {path to test file}`**: Points to the test file you want to run, e.g., `/src/tests/orders-email.js`.
3. **Script parameters**: Provided as environment variables for the container:
   ```bash
   -e tokenGeneratorUserName=***
   -e tokenGeneratorUserPwd=***
   -e env=***
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

```bash
$> k6 run /src/tests/orders-email.js \
    -e tokenGeneratorUserName=*** \
    -e tokenGeneratorUserPwd=*** \
    -e env=*** \
    --vus=10 \
    --duration=5m
```

### Notes for `orders-org-no.js`

The `orders-org-no.js` test file contains a list of different organization numbers when running the test in the `yt01` environment. For all other environments, the list contains only one element, allowing the test to run as before for functional testing.

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
