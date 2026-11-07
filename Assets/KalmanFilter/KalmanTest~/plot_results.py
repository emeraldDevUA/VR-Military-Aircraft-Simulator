# Plots from CSV in output/ (written by `dotnet run`). Run from the KalmanTest folder:  python3 plot_results.py
# Output: plots/<scenario>/1_position.png, 2_error.png, 3_velocity.png, 4_error_band.png
import os
import numpy as np
import pandas as pd
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

OUT, PLOTS = "output", "plots"
AXES = ["x", "y", "z"]
# zoom window, s: radar noise is invisible at full scale. Turn: its start, straight: radar gap (~12.5 s)
ZOOM = {"straight": (11.5, 14.0)}
ZOOM_DEFAULT = (10.0, 12.0)

prm = pd.read_csv(os.path.join(OUT, "params.csv")).iloc[0]     # settings from C#, to avoid duplication
SETTINGS = f"radar σ {prm.sigma_radar:g} m, σ_a {prm.sigma_a:g}"
summary = pd.read_csv(os.path.join(OUT, "summary.csv"))
names = dict(zip(summary.slug, summary.scenario))


def save(fig, *path):
    p = os.path.join(PLOTS, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    fig.tight_layout()
    fig.savefig(p, dpi=120)
    plt.close(fig)


def mark(a, d, lo=None, hi=None):
    """gray: warm-up (not in metrics), red: steps without radar"""
    a.axvspan(d.t.min() if lo is None else lo, prm.warmup, color="gray", alpha=0.12, lw=0, label="warm-up (not in metrics)")
    gap = d.meas_x.isna()
    starts, ends = d.t[gap & ~gap.shift(1, fill_value=False)], d.t[gap & ~gap.shift(-1, fill_value=False)]
    for k, (t0, t1) in enumerate(zip(starts, ends)):
        a.axvspan(t0, t1, color="tab:red", alpha=0.15, lw=0, label="no radar" if k == 0 else None)
    if lo is not None:
        a.set_xlim(lo, hi)


def norm(df, a, b):
    """row-wise length of (a_x - b_x, a_y - b_y, a_z - b_z)"""
    return np.sqrt(sum((df[f"{a}_{c}"] - df[f"{b}_{c}"]) ** 2 for c in AXES))


for slug, name in names.items():
    d = pd.read_csv(os.path.join(OUT, f"trace_{slug}.csv"))
    t = d.t

    # 1. Position vs time: dots = radar, thin = truth, bright = filter. Left: full flight, right: zoom.
    zoom = ZOOM.get(slug, ZOOM_DEFAULT)
    fig, ax = plt.subplots(3, 2, figsize=(13, 9), sharex="col")
    for i, c in enumerate(AXES):
        for j, (lo, hi) in enumerate([(t.min(), t.max()), zoom]):
            m = (t >= lo) & (t <= hi)
            a = ax[i, j]
            mark(a, d, lo, hi)
            a.scatter(t[m], d[f"meas_{c}"][m], s=6, color="gray", alpha=0.5, label="radar")
            a.plot(t[m], d[f"true_{c}"][m], color="black", lw=0.8, label="truth")
            a.plot(t[m], d[f"est_{c}"][m], color="tab:orange", lw=1.6, label="filter")
            a.set_ylabel(f"{c.upper()}, m")
            a.grid(alpha=0.3)
        ax[i, 1].set_title(f"zoom {zoom[0]:g}–{zoom[1]:g} s" if i == 0 else "")
    # full flight: same vertical span for X, Y, Z so axes are comparable; zoom: each axis fits its own data
    lims = [ax[i, 0].get_ylim() for i in range(3)]
    half = max(hi - lo for lo, hi in lims) / 2
    for i, (lo, hi) in enumerate(lims):
        ax[i, 0].set_ylim((lo + hi) / 2 - half, (lo + hi) / 2 + half)
    ax[0, 0].set_title(f"{name} ({SETTINGS})")
    ax[0, 0].legend(loc="best")
    ax[2, 0].set_xlabel("t, s"); ax[2, 1].set_xlabel("t, s")
    save(fig, slug, "1_position.png")

    # 2. Position error (3D): radar vs filter
    fig, a = plt.subplots(figsize=(11, 4))
    mark(a, d)
    a.plot(t, norm(d, "meas", "true"), color="gray", lw=0.7, alpha=0.7, label="|measurement − truth|")
    a.plot(t, norm(d, "est", "true"), color="tab:blue", lw=1.4, label="|estimate − truth|")
    a.set_title(f"{name}: position error"); a.set_xlabel("t, s"); a.set_ylabel("m")
    a.legend(); a.grid(alpha=0.3)
    save(fig, slug, "2_error.png")

    # 3. Velocity: true vs estimated (radar does not measure velocity, the filter infers it from positions)
    fig, ax = plt.subplots(3, 1, figsize=(11, 8), sharex=True)
    for i, c in enumerate(AXES):
        mark(ax[i], d)
        ax[i].plot(t, d[f"true_v{c}"], color="black", lw=0.8, label="truth")
        ax[i].plot(t, d[f"est_v{c}"], color="tab:orange", lw=1.4, label="filter")
        ax[i].set_ylabel(f"v{c}, m/s"); ax[i].grid(alpha=0.3)
    ax[0].set_title(f"{name}: velocity"); ax[0].legend(); ax[2].set_xlabel("t, s")
    save(fig, slug, "3_velocity.png")

    # 4. X error with ±2·PositionStd band: should stay inside almost all the time
    err, band = d.est_x - d.true_x, 2 * d.std_x
    after = d.t >= prm.warmup                                  # same as metrics: skip warm-up
    inside = (err[after].abs() < band[after]).mean() * 100
    fig, a = plt.subplots(figsize=(11, 4))
    mark(a, d)
    a.fill_between(t, -band, band, color="tab:green", alpha=0.2, label="±2·PositionStd")
    a.plot(t, err, color="tab:blue", lw=1, label="estimate − truth (X)")
    a.set_title(f"{name}: X error inside ±2σ — {inside:.0f}% (expected ≈ 95%)")
    a.set_xlabel("t, s"); a.set_ylabel("m"); a.legend(); a.grid(alpha=0.3)
    save(fig, slug, "4_error_band.png")

print(f"Done: {os.path.abspath(PLOTS)}")
