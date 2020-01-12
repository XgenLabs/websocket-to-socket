import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';

import { AppRoutingModule } from './app-routing.module';

// Services
import { WindowRefService } from '../services/window-ref.service';

// Components
import { AppComponent } from './app.component';
import { ChatComponent } from '../chat/chat.component';

@NgModule({
  declarations: [
    AppComponent,
    ChatComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule
  ],
  providers: [WindowRefService],
  bootstrap: [AppComponent]
})
export class AppModule { }
