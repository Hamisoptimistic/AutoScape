using MetroHub.Core.Models;

namespace MetroHub.Core.Interfaces;

public interface ITile
{
    TileModel Model { get; }
    void OnActivate();
    void OnTick();
    void OnDispose();
}

public interface IWidget : ITile
{
    string WidgetKind { get; }
    bool RequiresPeriodicTick { get; }
    int TickIntervalSeconds { get; }
}
