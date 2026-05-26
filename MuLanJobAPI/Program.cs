using MuLanJobAPI.Entity;
using MuLanJobAPI.Service;
using SqlSugar;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();


// 配置 SqlSugar
builder.Services.AddScoped<ISqlSugarClient>(s =>
{
    var sqlSugar = new SqlSugarClient(new ConnectionConfig()
    {
        ConnectionString = builder.Configuration.GetConnectionString("DefaultConnection"),
        DbType = SqlSugar.DbType.SqlServer,
        IsAutoCloseConnection = true
    });
    return sqlSugar;
});

// 注册 Redis（从你的 appsettings.json 读取！关键修复）
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    string redisConn = builder.Configuration["Redis:ConnectionString"];

    // 核心修复：添加配置，自动重试，不立即抛错
    var config = ConfigurationOptions.Parse(redisConn);
    config.AbortOnConnectFail = false;    // 允许重试，不立即崩溃
    config.ConnectTimeout = 5000;         // 连接超时 5 秒
    config.SyncTimeout = 5000;            // 操作超时
    config.KeepAlive = 60;                // 保活

    return ConnectionMultiplexer.Connect(config);
});

// 缓存服务
builder.Services.AddSingleton<IAppCacheService, RedisCacheService>();

// HttpContext
builder.Services.AddHttpContextAccessor();

builder.Services.Configure<WxConfig>(builder.Configuration.GetSection("WxConfig"));


builder.Services.AddScoped<LoginService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<FavoriteService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//app.MapOpenApi();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
});
//}

app.UseHttpsRedirection();
app.UseStaticFiles();
// 2. 静态文件中间件（不会再拦截 Swagger 路径）
app.UseStaticFiles();

// 3. CORS 跨域
app.UseCors(x => x
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

app.UseAuthorization();

app.MapControllers();

app.Run();
