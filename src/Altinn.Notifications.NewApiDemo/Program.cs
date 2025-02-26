using Microsoft.AspNetCore.Mvc;
using WebApplication1;
using Scalar.AspNetCore;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices.JavaScript;
using Altinn.Notifications.NewApiDemo.api.order.Response;
using Altinn.Notifications.NewApiDemo.api.Recipient.Notification;
using Altinn.Notifications.NewApiDemo.api.shared;
using Altinn.Notifications.NewApiDemo.api.status;
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

Dictionary<string, Guid> idempotencyToOrderRepo = new Dictionary<string, Guid>();
Dictionary<Guid, NotificationOrderResponse> orderRepo = new Dictionary<Guid, NotificationOrderResponse>();
Dictionary<Guid, Object> outboundMessageRepo = new Dictionary<Guid, Object>();

app.MapGet("/order/{notificationOrderId}",
    ([FromRoute] Guid notificationOrderId) =>
    {
        NotificationOrderResponse response;

        if (orderRepo.TryGetValue(notificationOrderId, out response))
        {
            return Results.Ok(response);
        }

        return Results.NotFound();
    });

app.MapPost("/order",
        ([FromBody] Notification notification) =>
        {
            //validation
            
            //known idempotencyId?
            
            Guid orderId;
            if (idempotencyToOrderRepo.TryGetValue(notification.IdempotencyId, out orderId))
            {
                NotificationOrderResponse existingResponse;
                if (orderRepo.TryGetValue(orderId, out existingResponse)){
                    return Results.Ok(existingResponse);
                }
                else
                {
                    return Results.InternalServerError(
                        "Inconsistent data-store state. IdempotencyId not found in idempotencyToOrderRepo, but order not found in orderRepo");
                }
            }
            
            //various validation - only simulated for now
            if (notification.NotificationRecipient.GetType().Equals(typeof(RecipientNationalIdentityNumber)) && ((RecipientNationalIdentityNumber)notification.NotificationRecipient).NationalIdentityNumber.Equals("00000000000"))
            {
                ProblemDetails validationProblem = new ProblemDetails();
                validationProblem.Title = "Validation failed";
                validationProblem.Status = StatusCodes.Status422UnprocessableEntity;
                validationProblem.Detail = "NationalIdentityNumber cannot be 00000000000";
                return Results.UnprocessableEntity(validationProblem);
            }
            
            //TODO: convert/normalize to outbound message
            
            //make 
            
            NotificationOrderResponse response = new()
            {
                
                NotificationOrderId = Guid.NewGuid(),
                Status = "Accepted",
                NotificationStatus = new NotificationStatus()
                {
                    NotificationId = Guid.NewGuid(),
                    SendersReference = notification.SendersReference,
                    
                    Reminders = (notification.Reminders ?? new List<Reminder>()).ConvertAll(r => new BaseNotificationStatus()
                    {
                        NotificationId = Guid.NewGuid(),
                        SendersReference = r.SendersReference,
                       
                    }).ToList()
                    
                
                }
            };
            
            idempotencyToOrderRepo.Add(notification.IdempotencyId, response.NotificationOrderId);
            orderRepo.Add(response.NotificationOrderId, response);
           //TODO: add outbound messages to repo
            

            DateTime plannedSendTime = notification.RequestedSendTime ?? DateTime.Now;

            Console.Out.WriteLine("Planning notification {0} for {1}", notification.SendersReference, plannedSendTime); 
            
            (notification.Reminders ?? new List<Reminder>()).ForEach(r => Console.Out.WriteLine("Planning reminder {0} for {1}", r.SendersReference, plannedSendTime.AddDays(r.RequestedSendTimeDelayDays ?? 0)));
                
            
            return Results.Created(string.Format("/order/{0}", response.NotificationOrderId ), response);
        })
    //.Accepts<Notification>("application/json") //json is default
    .Produces<NotificationOrderResponse>(StatusCodes.Status200OK)
    .Produces<NotificationOrderResponse>(StatusCodes.Status201Created)
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
    .Produces<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)
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
            operation.Responses["200"].Description = "The creation of the notification and all associated reminders associated with the provided idempotencyId was *already* successfully carried out. The response contains a list of all notifications and reminders *that was previously* created, with their respective ids. A 200 OK response indicates that whatever was in the current payload was ignored, in favour of the already created items.";
            operation.Responses["201"].Description = "The creation of the notification and all associated reminders was successful. The response contains a list of all notifications and reminders created, with their respective ids.";
            operation.Responses["400"].Description =
                "The provided data was not parsable. NB: The ProblemDetails object returned in this call will most likely change!";

            return operation;
        }
    );



app.MapGet("/status/shipment/{notificationId}",
    ([FromRoute] Guid notificationId) =>
    {
        return Results.NoContent();
    });


app.MapGet("/status/shipment/feed",
    ([FromQuery] int seq) =>
    {
        List<ShipmentStatus> statuses = new List<ShipmentStatus>();
        
        //for a random number of iterations between 10 and 1000
        Random rnd = new Random();
        int iterations = rnd.Next(10, 1000);

        for (int i = 0; i < iterations; i++)
        {
            ShipmentStatus status = new()
            {
                SequenceNumber = seq + i,
                ShipmentType = rnd.Next(0, 2) == 0? ShipmentType.Notification : ShipmentType.Reminder,
                
                NotificationId = Guid.NewGuid(),
                SendersReference = "Random-Senders-Reference-" + rnd.Next(1, 100000),
                Status = Enum.GetValues<ShipmentStatusType>()[rnd.Next(0, Enum.GetNames<ShipmentStatusType>().Length)],
                LastUpdated = DateTime.Now,
                
                Recipients = [new()
                {
                    Type = ShipmentRecipientType.Email,
                    Destintion = "navn.navnesen@example.com"
                }, new(){
                    Type = ShipmentRecipientType.SMS,
                    Destintion = "99999999"
                }],
            };
                
            statuses.Add(status);
        }
       
        
        return Results.Ok(statuses);
    });


app.Run();

