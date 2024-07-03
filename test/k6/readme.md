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
