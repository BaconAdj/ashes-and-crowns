"""Shared 1.75 m human body used by gen_man and gen_soldier. Slot 1 = cloth, slot 2 = skin (man) or iron (soldier)."""


def legs_and_tunic(b, bulk=1.0, skin_slot=True):
    for x in (-0.10, 0.10):
        b.box(x, 0.03, 0.0, 0.15, 0.27, 0.11)                                  # boots
        b.tube((x, 0.0, 0.10), (x, 0.0, 0.86), 0.085, 0.075, n=7)                # hose
    b.frustum(0, 0, 0.80, 0.46 * bulk, 0.30, 0.40 * bulk, 0.26, 0.32)           # tunic skirt, wider at the hem
    b.frustum(0, 0, 1.10, 0.40 * bulk, 0.26, 0.46 * bulk, 0.28, 0.38)           # chest to shoulders (1.48)
    b.box(0, 0, 1.08, 0.43 * bulk, 0.29, 0.05)                                  # belt
    b.tube((0, 0, 1.46), (0, 0, 1.55), 0.065, 0.06, n=7, roof=skin_slot)         # neck


def head(b, skin_slot=True):
    b.ellipsoid(0, 0.01, 1.64, 0.095, 0.11, 0.12, seg=9, rings=6, roof=skin_slot)   # skull, crown at 1.76
    b.ellipsoid(0, -0.02, 1.68, 0.10, 0.105, 0.095, seg=9, rings=5)              # hair, as a cloth-coloured cap


def arm(b, side, shoulder, elbow, hand, r=0.06, skin_slot=True):
    b.tube(shoulder, elbow, r, r * 0.9, n=6)
    b.tube(elbow, hand, r * 0.9, r * 0.75, n=6)
    b.ellipsoid(hand[0], hand[1], hand[2] - 0.04, 0.05, 0.06, 0.07, seg=6, rings=4, roof=skin_slot)
