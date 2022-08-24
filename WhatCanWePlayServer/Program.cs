using WhatCanWePlayServer;



var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

Database db = new Database("db.txt");
Dictionary<System.Net.IPAddress, DateTime> getRatelimit = new();
Dictionary<System.Net.IPAddress, DateTime> postRatelimit = new();

app.Use(async (context, next) =>
{
    //rate limiting by Ip

    if (context.Request.Method == "POST") // one post each 10 minutes
    {
        var hostIP = context.Request.HttpContext.Connection.RemoteIpAddress;
        if (postRatelimit.ContainsKey(hostIP))
        {
            if (DateTime.Now - postRatelimit[hostIP] < TimeSpan.FromSeconds(5))
            {
                //return Results.StatusCode(429);
                context.Response.StatusCode = 429;
                await context.Response.StartAsync();
                return;
            }
            else
            {
                postRatelimit[hostIP] = DateTime.Now;
            }
        }
        else
        {
            postRatelimit.Add(hostIP, DateTime.Now);
        }

        Console.WriteLine(context.Request.ContentLength);

        //limit post requests larger than 5kb
        if (context.Request.ContentLength > 5000)
        {
            context.Response.StatusCode = 413;
            await context.Response.StartAsync();
            return;
        }

    }
    /*
    else if (context.Request.Method == "GET") // one get request each 5 seconds
    {
        //ratelimit
        var hostIP = context.Request.HttpContext.Connection.RemoteIpAddress;
        if (getRatelimit.ContainsKey(hostIP))
        {
            if (DateTime.Now - getRatelimit[hostIP] < TimeSpan.FromSeconds(5))
            {
                //return Results.StatusCode(429);
                context.Response.StatusCode = 429;
                await context.Response.StartAsync();
                return;
            }
            else
            {
                getRatelimit[hostIP] = DateTime.Now;
            }
        }
        else
        {
            getRatelimit.Add(hostIP, DateTime.Now);
        }

    }

    */


    await next();
});



app.MapGet("/", () =>
{
    return Results.Content("<!DOCTYPE html><html><body>This is an API for my project, not an actual webpage (yet)<br>Check <a href=\"https://github.com/3ncy/WhatCanWePlay\">github.com/3ncy/WhatCanWePlay</a> for more info about the app.</body></html>", "text/html");
});

app.MapGet("/users/{id}", (Guid id, HttpRequest request) => //todo: tohle predelet asi na async a awaitovat volani database
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

    return Results.Created($"/users/{info.Id}", null);
});

app.MapPost("/ping", () =>
{
    return Results.Ok("Api seems to be responding.");
});


//app.MapGet("/t", (Guid[] ids) => {
//    return Results.Ok(ids.Length);
//});



app.Run();

record Info(string Id, string Value);