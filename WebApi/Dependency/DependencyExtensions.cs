﻿using Application;
using Common.Bases;
using Infra.Accounts.Repository;
using Infra.Interceptors;
using Infra.Users.Repository;
using Infra;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Core.Features.Accounts.Interfaces;
using Core.Features.Users.Interfaces;
using Microsoft.OpenApi.Models;
using Core.Features.Notification;
using Infra.Notification;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WebApi.Dependency;

public static class DependencyExtensions
{
    public static IServiceCollection RegisterWebApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });

        services.AddControllers()
                .AddFluentValidation(options => options.RegisterValidatorsFromAssemblyContaining(typeof(Program)));

        services.AddDbContext<appDbContext>(
                option => option.UseSqlServer(configuration["ConnectionStrings:default"])
                                .AddInterceptors(new mySaveChangesInterceptor())
            );

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
        });

        return services;
    }
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAccountReader, AccountReader>();
        services.AddScoped<INotificationService, NotificationService>();

        services.Scan(scan => scan.FromAssembliesOf(typeof(ApplicationAssembly))
                                              .AddClasses(classes => classes.AssignableTo<ITransient>())
                                              .AsImplementedInterfaces()
                                              .WithTransientLifetime());
        return services;
    }
    public static IServiceCollection RegisterEndpoints(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        var assembly = typeof(WebApiAssembly).Assembly;

        ServiceDescriptor[] serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this WebApplication app)
    {
        IEnumerable<IEndpoint> endpoints = app.Services
                                              .GetRequiredService<IEnumerable<IEndpoint>>();

        foreach (IEndpoint endpoint in endpoints)
        {
            endpoint.MapEndPoint(app);
        }

        return app;
    }
}
