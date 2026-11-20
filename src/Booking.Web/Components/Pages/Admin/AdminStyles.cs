namespace Booking.Web.Components.Pages.Admin;

/// <summary>Tailwind class strings shared by the admin pages (written out in full so the Tailwind build finds them).</summary>
internal static class AdminStyles
{
    private const string Disabled = " disabled:cursor-not-allowed disabled:opacity-40";
    private const string Focus = " focus:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400";

    public const string Panel = "mb-4 flex flex-wrap items-end gap-3 rounded-xl bg-white/5 p-4 text-sm ring-1 ring-white/10";
    public const string Label = "flex flex-col gap-1";
    public const string LabelText = "text-xs text-slate-400";
    public const string Input = "rounded-lg bg-slate-900 px-3 py-2 ring-1 ring-white/10 focus:outline-none focus:ring-fuchsia-400";
    public const string CellInput = "w-full min-w-24 rounded-lg bg-slate-900 px-2 py-1 ring-1 ring-fuchsia-400/50 focus:outline-none focus:ring-fuchsia-400";
    public const string CellSelect = "min-w-36 rounded-lg bg-slate-900 px-2 py-1 text-sm ring-1 ring-white/10 focus:outline-none focus:ring-fuchsia-400";

    public const string Error = "mb-4 rounded-xl bg-rose-500/10 px-4 py-2.5 text-sm text-rose-200 ring-1 ring-rose-500/30";

    public const string TableWrap = "relative overflow-x-auto rounded-xl bg-white/5";
    public const string Table = "w-full text-sm";
    public const string Head = "border-b border-white/10 text-xs uppercase tracking-wider text-slate-500";
    public const string Th = "px-4 py-2 text-left font-normal";
    public const string Td = "px-4 py-2.5";

    public const string Primary = "rounded-lg bg-fuchsia-500 px-4 py-2 font-semibold hover:bg-fuchsia-400" + Focus + Disabled;
    public const string SmallPrimary = "rounded-lg bg-fuchsia-500 px-3 py-1 text-xs font-semibold hover:bg-fuchsia-400" + Focus + Disabled;
    public const string SmallSecondary = "rounded-lg px-3 py-1 text-xs ring-1 ring-white/20 hover:bg-white/10" + Focus + Disabled;
    public const string SmallLink = "px-2 py-1 text-xs text-slate-400 hover:text-slate-200" + Focus;

    /// <summary>A checkbox drawn as an on/off switch; still a real checkbox for keyboard and screen readers.</summary>
    public const string Switch =
        "relative h-5 w-9 shrink-0 cursor-pointer appearance-none rounded-full bg-slate-700 transition-colors checked:bg-emerald-500 " +
        "before:absolute before:top-0.5 before:left-0.5 before:h-4 before:w-4 before:rounded-full before:bg-slate-400 before:transition-transform " +
        "checked:before:translate-x-4 checked:before:bg-white " +
        "focus:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 focus-visible:ring-offset-2 focus-visible:ring-offset-slate-950";
}
