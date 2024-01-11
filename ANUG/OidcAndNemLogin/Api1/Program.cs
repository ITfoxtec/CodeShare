using Api1.Models;
using Api1.Policies;
using ITfoxtec.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Logging;

var builder = WebApplication.CreateBuilder(args);

var identitySettings = builder.Services.BindConfig<IdentitySettings>(builder.Configuration, nameof(IdentitySettings));

IdentityModelEventSource.ShowPII = true; //To show detail of error and see the problem

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = identitySettings.FoxIDsAuthority;
        options.Audience = identitySettings.ResourceId;

        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = JwtClaimTypes.Subject;
        options.TokenValidationParameters.RoleClaimType = JwtClaimTypes.Role;

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async (context) =>
            {
                await Task.FromResult(string.Empty);
            },
            OnAuthenticationFailed = async (context) =>
            {
                // TODO log error
                await Task.FromResult(string.Empty);
            },
            OnForbidden = async (context) =>
            {
                // TODO log error / response code
                await Task.FromResult(string.Empty);
            }
        };
    });

// Access policy
builder.Services.AddAuthorization(Api1AccessAuthorizeAttribute.AddPolicy);

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
