<!--
title: Websocket to socket
layout: Doc
-->
# Objective

This example was built how to create a tunnel between a websocket connection to a socket connection with support to send and receive data on both sides.

## Use-cases

- Create a websocket server to run front of an existing socket server as an adapter.

## Pre-Requirements
- Angular 8 or later (https://angular.io/cli)
- .NET Core SDK 3.0.1 (https://dotnet.microsoft.com/download)

## How to run

1. **Site**
  ```bash
  npm install
  # After the npm packages was installed successfully

  ng serve
  ```

2. **TCP Server**
  ```bash
  dotnet restore && donet run
  ```

3. **Websocket Server**
  ```bash
  dotnet restore && donet run
  ```