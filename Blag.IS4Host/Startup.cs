// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Blag.IS4Host.Data;
using Blag.IS4Host.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Linq;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.EntityFramework.DbContexts;

namespace Blag.IS4Host
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // Store connection string as a var
            var connectionString = Configuration.GetConnectionString("DefaultConnection");

            // Store assembly for migrations
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            // Replace DBCOntext database from SqlLite in template to Postgres
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc();

            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });

            var builder = services.AddIdentityServer()

                // Use our Postgres database for storing configuration data
                .AddConfigurationStore(configDb =>
                {
                    configDb.ConfigureDbContext = db => db.UseNpgsql(connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                // Use our Postgres database for storing operational data
                .AddOperationalStore(operationalDb =>
                {
                    operationalDb.ConfigureDbContext = db => db.UseNpgsql(connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                .AddAspNetIdentity<ApplicationUser>();

            if (Environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                throw new Exception("need to configure key material");
            }

            services.AddAuthentication()
                        .AddGoogle(options =>
                        {
                            options.ClientId = "708996912208-9m4dkjb5hscn7cjrn5u0r4tbgkbj1fko.apps.googleusercontent.com";
                            options.ClientSecret = "wdfPY6t8H8cecgjlxud__4Gh";
                        });
        }

        public void Configure(IApplicationBuilder app)
        {
            InitializeDatabase(app);

            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }

        private void InitializeDatabase(IApplicationBuilder app)
        {
            using(var serviceScope = 
                app.ApplicationServices.GetService<IServiceScopeFactory>()
                .CreateScope())
            {
                // Create PersistedGrant Database (using single db here,
                // if does not exist run outstanding migrations)
                var persistedGrantDbContext = serviceScope.ServiceProvider
                    .GetRequiredService<PersistedGrantDbContext>();
                persistedGrantDbContext.Database.Migrate();

                var configDbContext = serviceScope.ServiceProvider
                    .GetRequiredService<ConfigurationDbContext>();
                configDbContext.Database.Migrate();
                
                if(!configDbContext.Clients.Any()){
                    foreach(var client in Config.GetClients()){
                        configDbContext.Clients.Add(client.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }
                
                if(!configDbContext.IdentityResources.Any()){
                    foreach(var res in Config.GetIdentityResources()){
                        configDbContext.IdentityResources.Add(res.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }
                
                if(!configDbContext.ApiResources.Any()){
                    foreach(var api in Config.GetApis()){
                        configDbContext.ApiResources.Add(api.ToEntity());
                    }
                    configDbContext.SaveChanges();
                }
            }
        }
    }
}