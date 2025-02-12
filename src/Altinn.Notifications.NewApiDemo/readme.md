Warning: this project ("NewApiDemo") is unstable and work-in-progress. It is to capture early feedback and ideas for a new API design for Altinn.

The below only pertains to the NewApiDemo mock, and will not build/run Notifications as such.


Build the mock-app by building the docker image. The following command will build both the app and image:
```
docker build -t notification-mock .
```

The container exposes the app on an unauthenticated http-server on port 5151 (currently hard-coded, don't change this if you want Swagger/Scalar to work!). 
To run the container, use the following command:
```
docker run -p 127.0.0.1:5151:5151 notification-mock
```


Then you should be able to open the OpenAPI-doc through either Scalar (http://localhost:5151/scalar/v2) or Swagger (http://localhost:5151/swagger/index.html). 
An OpenAPI spec document is available at http://localhost:5151/openapi/v2.json 

There is a Bruno (https://www.usebruno.com/) collection in [../../test/Altinn.Notifications.NewApiDemoTest/bruno](../../test/Altinn.Notifications.NewApiDemoTest/bruno) with examples/testcases for the API.

Known issues:
* Some of the text in the spec is off (e.g. the Swagger-heading "Altinn.Notifications.NewApiDemo | v1" referencing "v1" etc.)
* Mock endpoint is not built out with validation