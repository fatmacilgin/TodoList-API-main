# 1. Derleme (Build) Aþamasý
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje dosyalarýný kopyala (Katman isimlerinle birebir ayný olmalý)
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
COPY --from=publish /app/publish .

# Render'ýn dinleyeceði varsayýlan port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# 2. Çalýþtýrma (Runtime) Aþamasý
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Status 139 (Segmentation Fault) engelleyici globalization ayarý:
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TodoList.WebApi.dll"]