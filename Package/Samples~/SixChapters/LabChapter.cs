using UnityEngine;

internal abstract class LabChapter : MonoBehaviour
{
    LabGame game;
    protected Camera Eye => game.Eye;
    protected bool IsWallStudy => game.WallStudy;
    protected string Saved => game.Snapshot;
    public abstract string Hint { get; }
    public abstract bool Complete { get; }

    internal void Initialize(LabGame owner)
    {
        game = owner;
        Open();
    }

    protected abstract void Open();
    public abstract bool Tap(Vector2 screen);
    public virtual bool StartDrag(Vector2 origin) => false;
    public virtual void MoveDrag(Vector2 point) { }
    public virtual void EndDrag() { }
    public virtual void Panel() { }
    public virtual void Overlay() { }
    public virtual void Suspend() { }
    protected bool ActionButton(string label) => game.ActionButton(label);
    protected void Save(string json) => game.SaveSnapshot(json);
    protected void Result(string message) => game.Log(message);
    protected void Cue(Vector2 screen, string label, Vector2 offset) => game.Cue(screen, label, offset);

    protected void View(Vector3 focus, Vector3 euler, float orthographicSize, float distance = 12)
    {
        Eye.orthographicSize = orthographicSize;
        Eye.transform.rotation = Quaternion.Euler(euler);
        Eye.transform.position = focus - Eye.transform.forward * distance;
    }
}
