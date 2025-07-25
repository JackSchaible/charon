import { ComponentFixture, TestBed } from '@angular/core/testing';

import { WishlistProcessing } from './wishlist-processing';

describe('WishlistProcessing', () => {
  let component: WishlistProcessing;
  let fixture: ComponentFixture<WishlistProcessing>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WishlistProcessing]
    })
    .compileComponents();

    fixture = TestBed.createComponent(WishlistProcessing);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
