# 1. Derleme (Build) Aþamasý
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TodoList.WebApi/TodoList.WebApi.csproj", "TodoList.WebApi/"]
COPY ["TodoList.DataAccess/TodoList.DataAccess.csproj", "TodoList.DataAccess/"]
COPY ["TodoList.Business/TodoList.Business.csproj", "TodoList.Business/"]

RUN dotnet restore "TodoList.WebApi/TodoList.WebApi.csproj"
COPY . .
WORKDIR "/src/TodoList.WebApi"
RUN dotnet build "TodoList.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TodoList.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Çalýþtýrma (Runtime) Aþamasý
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Native kütüphane kilitlenmelerini önleyen paket
RUN apt-get update && apt-get install -y libicu-dev && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TodoList.WebApi.dll"]