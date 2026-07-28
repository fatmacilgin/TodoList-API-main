FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "TodoList.WebApi/TodoList.WebApi.csproj"
RUN dotnet publish "TodoList.WebApi/TodoList.WebApi.csproj" -c Release -o /app/out

# YANLIÞ: FROM mcr.microsoft.com/dotnet/aspnet:10.0
# DOÐRU:
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TodoList.WebApi.dll"]