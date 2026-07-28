# Runtime imajý (.NET 10 Preview)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app

# SDK imajý (.NET 10 Preview)
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Proje kopyalama ve restore iþlemleri
COPY ["TodoList.WebApi/TodoList.WebApi.csproj", "TodoList.WebApi/"]
COPY ["TodoList.DataAccess/TodoList.DataAccess.csproj", "TodoList.DataAccess/"]
COPY ["TodoList.Business/TodoList.Business.csproj", "TodoList.Business/"]
COPY ["TodoList.Entities/TodoList.Entities.csproj", "TodoList.Entities/"]

RUN dotnet restore "TodoList.WebApi/TodoList.WebApi.csproj"
COPY . .
WORKDIR "/src/TodoList.WebApi"
RUN dotnet publish "TodoList.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TodoList.WebApi.dll"]