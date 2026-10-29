import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { iniciarFaro } from './app/core/observabilidad/faro';

bootstrapApplication(App, appConfig)
  .then(() => iniciarFaro())
  .catch((err) => console.error(err));
