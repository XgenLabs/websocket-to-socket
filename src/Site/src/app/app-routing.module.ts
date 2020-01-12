import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';
import { ChatComponent } from 'src/chat/chat.component';


const routes: Routes = [
  {
    path: '',
    redirectTo: 'chat',
    pathMatch: 'full',
  },
  { path: 'chat', component: ChatComponent }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
