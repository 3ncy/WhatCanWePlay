using WhatCanWePlayServer;



var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

Database db = new Database("db.txt");

app.MapGet("/", () =>
{
    return Results.Content("<!DOCTYPE html><html><body>This is an API for my project, not an actual webpage (yet)<br>Check <a href=\"https://github.com/3ncy/WhatCanWePlay\">github.com/3ncy/WhatCanWePlay</a> for more info about the app.</body></html>", "text/html");
});

app.MapGet("/users/{id}", (Guid id) => //todo: tohle predelet asi na async a awaitovat volani database
{
    string data = db.Get(id.ToString());

    Console.WriteLine($"data pro guid {id}: '{data}'");

    if (data == "")
    {
        Console.WriteLine("not found");
        return Results.NotFound(); //no games found for this user guid
    }

    return Results.Ok(data);
});



app.MapPost("/users", (Info info, HttpRequest request) =>
{
    Console.WriteLine($"post: guid: {info.Id}, value: {info.Value}");
    db.Save(info.Id.ToString(), info.Value);
    return Results.Ok(); //tohle by melo byt 201 - Created, ale fakt se mi to nechce implementovat
});

app.MapPost("/ping", () =>
{
    return Results.Ok("Api seems to be responding.");
});


app.Run();

record Info(string Id, string Value);