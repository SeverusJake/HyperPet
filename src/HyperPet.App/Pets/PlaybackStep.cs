using HyperPet.Core.Pets;

namespace HyperPet.App.Pets;

/// <summary>
/// Pure frame-advance math for one animator tick. Returns the next frame
/// index, the next direction (for PingPong), and whether a non-looping pass
/// has reached its natural end. Keeps the index on the final frame when the
/// pass completes (callers that loop decide whether to wrap).
/// </summary>
public readonly record struct PlaybackResult(int Index, int Direction, bool Completed);

public static class PlaybackStep
{
    public static PlaybackResult Next(PlayMode mode, int index, int direction, int frameCount)
    {
        if (frameCount <= 1)
        {
            return new PlaybackResult(0, direction, true);
        }

        int last = frameCount - 1;

        switch (mode)
        {
            case PlayMode.Reverse:
                if (index <= 0)
                {
                    return new PlaybackResult(0, direction, true);
                }
                return new PlaybackResult(index - 1, direction, false);

            case PlayMode.PingPong:
                int next = index + direction;
                if (next > last)
                {
                    return new PlaybackResult(Math.Max(0, last - 1), -1, true);
                }
                if (next < 0)
                {
                    return new PlaybackResult(Math.Min(last, 1), 1, true);
                }
                return new PlaybackResult(next, direction, false);

            case PlayMode.Forward:
            default:
                if (index >= last)
                {
                    return new PlaybackResult(last, direction, true);
                }
                return new PlaybackResult(index + 1, direction, false);
        }
    }
}
