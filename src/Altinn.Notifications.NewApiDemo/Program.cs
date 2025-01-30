using Microsoft.AspNetCore.Mvc;
using WebApplication1;
using Scalar.AspNetCore;
using System.ComponentModel;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var  anyOrigin = "_anyOrigin";


// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Always inline enum schemas
    options.CreateSchemaReferenceId = (type) => type.Type.IsEnum ? null : OpenApiOptions.CreateDefaultSchemaReferenceId(type);
    options.AddDocumentTransformer((doc, tCtx, cancelToken) =>
    {
        doc.Servers = new List<OpenApiServer>
        {
            new OpenApiServer
            {
                Url = "http://localhost:5151",
                Description = "Localhost"
            }
        };
        return Task.CompletedTask;
    });
    
    // c.EnableAnnotations(enableAnnotationsForInheritance: true, enableAnnotationsForPolymorphism: true);
});


builder.Services.AddProblemDetails();


var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    
    app.MapOpenApi("/openapi/v2.json");
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v2.json", "v2");
        
    });
    
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();


app.MapPost("/notification",
        ([FromBody] NotificationOrder notification) =>
        {
            //validation
            if (notification.Notifications == null || notification.Notifications.Count == 0)
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "No notifications provided",
                    Detail = "No notifications were provided in the request"
                });
            }
            //filter notifications for all elements of type Notification
            
            
            if (notification.Notifications.FindAll(notification1 => notification1.NotificationType == NotificationType.Notification).Count != 1) 
            {
                return Results.BadRequest(new ProblemDetails
                {
                    Title = "NotificationType.Notification can be excactly 1",
                    Detail = "Too many or too few notifications of type 'Notification' was provided in the request"
                });
            }
            
            
            List<NotificationResponse> responses = new();
            
            notification.Notifications.ForEach(n =>
            {
                NotificationResponse response = new()
                {
                    NotificationId = Guid.NewGuid(),
                    SendersReference = n.SendersReference,
                    NotificationType = n.NotificationType
                };
                responses.Add(response);
            });
            
            return Results.Ok(responses);
        })
    .Produces<List<NotificationResponse>>(StatusCodes.Status200OK)
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
    .WithName("CreateNotification")
    .WithSummary("Create a notification")
    .WithDescription(
        """
        Description of operation goes here
        
        Response 400 - Bad Request: The ProblemDetail will most likely change to a custom contract with less details exposed externally
        """
    )

    //From https://stackoverflow.com/questions/77438799/override-response-description-in-minimal-api
    //....but seems to not be working
    
    .WithOpenApi(operation =>
        {
            operation.Responses["200"].Description = "The creation of the notification and all associated reminders were successfull. The response contains a list of all notifications and reminders created, with their respective ids.";
            operation.Responses["400"].Description =
                "The provided data was not parsable. NB: The ProblemDetails object returned in this call will most likely change!";

            return operation;
        }
    );


app.Run();

