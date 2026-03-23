FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copy csproj and restore
COPY ["TemperatureApi.csproj", "./"]
RUN dotnet restore "TemperatureApi.csproj"

# copy everything and publish
COPY . .
RUN dotnet publish "TemperatureApi.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "TemperatureApi.dll"]