﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleWeather.UWP.Controls.Graphs
{
    public class BarGraphData : GraphData<BarGraphDataSet, BarGraphEntry>
    {
        public BarGraphData() : base() { }

        public BarGraphData(BarGraphDataSet set) : base(set) { }

        public BarGraphData(string label, BarGraphDataSet set) : base(set) 
        {
            this.GraphLabel = label;
        }

        public void SetDataSet(BarGraphDataSet set)
        {
            DataSets.Clear();
            DataSets.Add(set);
            NotifyDataChanged();
        }

        public BarGraphDataSet GetDataSet()
        {
            return DataSets?.FirstOrDefault();
        }
    }
}
