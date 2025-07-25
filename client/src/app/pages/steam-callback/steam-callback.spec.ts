import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SteamCallback } from './steam-callback';

describe('SteamCallback', () => {
  let component: SteamCallback;
  let fixture: ComponentFixture<SteamCallback>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SteamCallback]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SteamCallback);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
