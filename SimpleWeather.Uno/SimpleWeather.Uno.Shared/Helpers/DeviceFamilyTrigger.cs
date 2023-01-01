﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace SimpleWeather.UWP.Shared.Helpers
{
    class DeviceFamilyTrigger : StateTriggerBase
    {
        private DeviceTypeHelper.DeviceTypes _actualDeviceFamily;
        private DeviceTypeHelper.DeviceTypes _triggerDeviceFamily;

        public DeviceTypeHelper.DeviceTypes DeviceFamily
        {
            get { return _triggerDeviceFamily; }
            set
            {
                _triggerDeviceFamily = value;
                _actualDeviceFamily = DeviceTypeHelper.DeviceType;
                SetActive(_triggerDeviceFamily == _actualDeviceFamily);
            }
        }
    }
}