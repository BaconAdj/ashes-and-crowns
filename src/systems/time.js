import balance from '../../data/balance.json';

const T = balance.time;

const MONTHS = ['January','February','March','April','May','June',
                'July','August','September','October','November','December'];

const SEASONS = {
  0:'Winter', 1:'Winter', 2:'Spring', 3:'Spring', 4:'Spring',
  5:'Summer', 6:'Summer', 7:'Summer', 8:'Autumn', 9:'Autumn', 10:'Autumn', 11:'Winter'
};

export class TimeSystem {
  constructor() {
    this.hour   = T.startHour;       // 0-24 float
    this.day    = T.startDay;        // 1-28
    this.month  = T.startMonth - 1;  // 0-indexed
    this.year   = T.startYear;
    this.realSecPerGameHour = T.realSecondsPerGameHour;
    this._lastBell = -1;
    this.listeners = [];
  }

  update(dt) {
    this.hour += (dt / this.realSecPerGameHour);
    if (this.hour >= 24) {
      this.hour -= 24;
      this.day++;
      if (this.day > 28) { // simplified medieval calendar
        this.day = 1;
        this.month = (this.month + 1) % 12;
        if (this.month === 0) this.year++;
      }
    }
    this._checkBells();
  }

  _checkBells() {
    for (let i = 0; i < T.bellHours.length; i++) {
      const bh = T.bellHours[i];
      if (this.hour >= bh && this.hour < bh + 0.25 && this._lastBell !== bh) {
        this._lastBell = bh;
        this.listeners.forEach(fn => fn(T.bellNames[i], bh));
      }
    }
    // Reset lastBell at end of day
    if (this.hour < 6 && this._lastBell > 0) this._lastBell = -1;
  }

  onBell(fn) { this.listeners.push(fn); }

  get bellName() {
    for (let i = T.bellHours.length - 1; i >= 0; i--) {
      if (this.hour >= T.bellHours[i]) return T.bellNames[i];
    }
    return 'Matins';
  }

  get dateString() {
    return `${MONTHS[this.month]}, ${this.year} A.D.`;
  }

  get season() { return SEASONS[this.month]; }

  get isDay() { return this.hour >= T.dawnHour && this.hour < T.duskHour; }
  get isNight() { return !this.isDay; }

  // 0-1 daylight factor
  get daylightFactor() {
    const h = this.hour;
    if (h < 6 || h > 21) return 0;
    return Math.sin((h - 6) / 15 * Math.PI);
  }
}
