import { Component, OnInit } from '@angular/core';
import * as signalR from "@aspnet/signalr";

import { WindowRefService } from 'src/services/window-ref.service';

@Component({
  selector: 'app-chat',
  templateUrl: './chat.component.html',
  styleUrls: ['./chat.component.scss']
})
export class ChatComponent implements OnInit {

  private hubConnection: signalR.HubConnection

  constructor(private window: WindowRefService) { }

  ngOnInit() {
    // let username: string

    // do {
    //   username = this.window.browserWindow.prompt('What\'s your name?')
    // } while (!username)

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(`http://localhost:5000/chat?username=john.doe`)
      .configureLogging(signalR.LogLevel.Debug)
      .build()

    this.hubConnection.onclose(error => console.log('disconnected from the server.', error))

    this.hubConnection
      .start()
      .then(connection => {
        console.log('Connection started')
      })
      .catch(err => console.log('Error while starting connection: ' + err))

      this.hubConnection.on("Update", (message) => {
        console.log(`(${new Date().toISOString()}) new message from server: ${message}`);
      });
  }
}
