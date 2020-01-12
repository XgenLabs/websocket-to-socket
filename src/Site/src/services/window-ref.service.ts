import { Injectable } from '@angular/core';

function getWindow (): any {
  return window;
}

@Injectable()
export class WindowRefService {
  get browserWindow (): any {
    return getWindow();
  }
}
