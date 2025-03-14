# Automated tests using k6

## Install prerequisites

*We recommend running the tests through a docker container.*

From the command line:

```
docker pull grafana/k6
```

Further information on [installing k6 for running in docker is available here.](https://k6.io/docs/get-started/installation/#docker)

Alternatively, it is possible to run the tests directly on your machine as well.

[General installation instructions are available here.](https://k6.io/docs/get-started/installation/)


## Running tests

All tests are defined in `src/tests` and in the top of each test file an example of the cmd to run the test is available.

The command should be run from the k6 folder.

```
$> cd /altinn-notifications/test/k6
```

Run test suite by specifying filename.

For example:

 ```
 $> podman compose run k6 run /src/tests/orders_email.js `
    -e tokenGeneratorUserName=*** `
    -e tokenGeneratorUserPwd=*** `
    -e env=*** `
    -e emailRecipient=*** `
    -e ninRecipient=*** `
    -e runFullTestSet=true
```

The command consists of three parts

`podman compose run` to run the test in a docker container

`k6 run {path to test file}` pointing to the test file you want to run e.g. `/src/tests/orders_email.js`

The last part is all script parameters provided as environment variables for the container.
```
 -e tokenGeneratorUserName=***
 -e tokenGeneratorUserPwd=***
 -e env=***
 -e emailRecipient=***
 -e ninRecipient=***
 -e runFullTestSet=true
````

## Load tests

The same tests will be used to run load and performance tests. Can be run a described above, just expand the commands using --vus, --duration/--iterations. Also run without runFullTestSet (or set to false).
Example command running directly on your machine, using 10 virtual users (vus) for 5 minutes:
```
 $> k6 run /src/tests/orders_email.js `
    -e tokenGeneratorUserName=*** `
    -e tokenGeneratorUserPwd=*** `
    -e env=*** `
    --vus=10
    --duration=5m
```
The `orders-org-no.js` is changed to use a list of different organization numbers when running the test in `yt01`. For all other environments, the list has only one element, to run as before for functional tests.

### Running load tests from github actions
A workflow_dispatch action is created in github. To run, follow these steps: .github/workflows/performance-test.yaml
1. Go to the [GitHub Actions](https://github.com/altinn/altinn-notifications/actions/workflows/performance-test.yml) page.
2. Select "Run workflow" and fill in the required parameters.
3. Tag the performance test with a descriptive name. 

The test will be executed in a k8s cluster with k6 operator, in a designated namespace.

### Load test results
Test results from github actions load test runs can be found in GitHub action run logs and grafana
