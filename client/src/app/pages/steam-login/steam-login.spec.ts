import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SteamLogin } from './steam-login';

describe('SteamLogin', () => {
  let component: SteamLogin;
  let fixture: ComponentFixture<SteamLogin>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SteamLogin]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SteamLogin);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
