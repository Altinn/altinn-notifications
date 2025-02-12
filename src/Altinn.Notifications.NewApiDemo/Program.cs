using Microsoft.AspNetCore.Mvc;
using WebApplication1;
using Scalar.AspNetCore;
using System.ComponentModel;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);


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


app.MapPost("/order",
        ([FromBody] Notification notification) =>
        {
            //validation
            
            NotificationOrderResponse response = new()
            {
                
                NotificationOrderId = Guid.NewGuid(),
                NotificationResponse = new NotificationResponse()
                {
                    NotificationId = Guid.NewGuid(),
                    SendersReference = notification.SendersReference,
                    
                    Reminders = (notification.Reminders ?? new List<Reminder>()).ConvertAll(r => new BaseNotificationResponse()
                    {
                        NotificationId = Guid.NewGuid(),
                        SendersReference = r.SendersReference
                    }).ToList()
                    
                
                }
            };

            DateTime plannedSendTime = notification.RequestedSendTime ?? DateTime.Now;

            Console.Out.WriteLine("Planning notification {0} for {1}", notification.SendersReference, plannedSendTime); 
            
            (notification.Reminders ?? new List<Reminder>()).ForEach(r => Console.Out.WriteLine("Planning reminder {0} for {1}", r.SendersReference, plannedSendTime.AddDays(r.RequestedSendTimeDelayDays ?? 0)));
                
            
            return Results.Ok(response);
        })
    //.Accepts<Notification>("application/json") //json is default
    .Produces<NotificationOrderResponse>(StatusCodes.Status200OK)
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

