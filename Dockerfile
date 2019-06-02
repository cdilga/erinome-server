FROM microsoft/dotnet:2.2-aspnetcore-runtime-alpine
COPY /deploy /
WORKDIR /Server
EXPOSE 5005
ENTRYPOINT [ "dotnet", "Server.dll" ]