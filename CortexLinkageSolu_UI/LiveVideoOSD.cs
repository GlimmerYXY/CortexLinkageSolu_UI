using System;

namespace CortexLinkageSolu_UI
{
    public class ST_AlarmInfo
    {
        public string CustomerNumber;
        public string FeedNumber;
        public bool IsAlarm;
        public string AlarmID;
        public string AlarmName;
        public DateTime AlarmStartTime;
        public DateTime AlarmStopTime;
        public int mode;
        public int frequency;

        public void setInfo(ST_AlarmInfo tmp)
        {
            this.CustomerNumber = tmp.CustomerNumber;
            this.FeedNumber = tmp.FeedNumber;
            this.IsAlarm = tmp.IsAlarm;
            this.AlarmID = tmp.AlarmID;
            this.AlarmName = tmp.AlarmName;
            this.AlarmStartTime = tmp.AlarmStartTime;
            this.AlarmStopTime = tmp.AlarmStopTime;
            this.mode = tmp.mode;
            this.frequency = tmp.frequency;
        }
    }
    
    public class confSWS
    {
        public string alarmID;
        public int mode;
        public int frequency;

        public confSWS(string alarmID, int modeForRuqin, int frequencyForRuqin)
        {
            this.alarmID = alarmID;
            this.mode = modeForRuqin;
            this.frequency = frequencyForRuqin;
        }
    }
}
