FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["HRAttendance.API/HRAttendance.API.csproj", "HRAttendance.API/"]
COPY ["HRAttendance.Application/HRAttendance.Application.csproj", "HRAttendance.Application/"]
COPY ["HRAttendance.Domain/HRAttendance.Domain.csproj", "HRAttendance.Domain/"]
COPY ["HRAttendance.Infrastructure/HRAttendance.Infrastructure.csproj", "HRAttendance.Infrastructure/"]
RUN dotnet restore "HRAttendance.API/HRAttendance.API.csproj"
COPY . .
WORKDIR "/src/HRAttendance.API"
RUN dotnet publish "HRAttendance.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HRAttendance.API.dll"]
