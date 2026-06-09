import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config'; // (zoneless + HttpClient)
import { App } from './app/app';//  (root shell, just renders <app-quotes>)

bootstrapApplication(App, appConfig)
  .catch((err) => console.error(err));
