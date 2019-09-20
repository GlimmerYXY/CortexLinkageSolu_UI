using System;

namespace CortexLinkageSolu_UI
{
    public class AlarmInfo
    {
        public string customerNumber;
        public string feedNumber;
        public bool isAlarm;
        public string alarmID;
        public string alarmName;
        public DateTime alarmStartTime;
        public DateTime alarmStopTime;
        public int mode;
        public int frequency;

        public void SetInfo(AlarmInfo tmp)
        {
            this.customerNumber = tmp.customerNumber;
            this.feedNumber = tmp.feedNumber;
            this.isAlarm = tmp.isAlarm;
            this.alarmID = tmp.alarmID;
            this.alarmName = tmp.alarmName;
            this.alarmStartTime = tmp.alarmStartTime;
            this.alarmStopTime = tmp.alarmStopTime;
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
