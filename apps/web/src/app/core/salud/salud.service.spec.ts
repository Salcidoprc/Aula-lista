import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SaludService } from './salud.service';

describe('SaludService', () => {
  let servicio: SaludService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    servicio = TestBed.inject(SaludService);
    http = TestBed.inject(HttpTestingController);
  });

  it('marca el servicio en línea cuando /salud responde', async () => {
    const revision = servicio.revisar();
    http.expectOne('/salud').flush('Healthy');
    await revision;
    expect(servicio.estado()).toBe('enLinea');
  });

  it('marca sin servidor cuando /salud falla', async () => {
    const revision = servicio.revisar();
    http.expectOne('/salud').flush('', { status: 503, statusText: 'Unavailable' });
    await revision;
    expect(servicio.estado()).toBe('sinServidor');
  });
});
