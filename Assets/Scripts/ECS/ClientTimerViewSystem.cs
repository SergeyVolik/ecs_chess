using Unity.Entities;

public static class TextHelper
{
    public static string TimeText(float seconds)
    {
        string text = "";
       
        int sec = (int)seconds;

        int minutes = (int)(seconds / 60);
        int hours = (int)(minutes / 60);

        if (hours >= 1)
        {
            minutes = (int)(minutes % 60);
            text += $"{hours} h ";
        }

        if (minutes >= 1)
        {
            sec = (int)(seconds % 60);
            text += $"{minutes} m ";
        }

        if (sec >= 1)
        {
            text += $"{sec} s ";
        }

        return text;
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class ClientTimerViewSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var item in SystemAPI.Query<ChessBoardTimerC>())
        {
            UIPages.Instance.gameUi.timeText.text = TextHelper.TimeText(item.duration);
        }
    }
}
