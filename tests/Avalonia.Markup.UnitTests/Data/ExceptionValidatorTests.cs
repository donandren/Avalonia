// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Data;
using Avalonia.Markup.Data.Plugins;
using Xunit;

namespace Avalonia.Markup.UnitTests.Data
{
    public class ExceptionValidatorTests
    {
        public class Data : INotifyPropertyChanged
        {
            private int nonValidated;

            public int NonValidated
            {
                get { return nonValidated; }
                set { nonValidated = value; NotifyPropertyChanged(); }
            }

            private int mustBePositive;

            public int MustBePositive
            {
                get { return mustBePositive; }
                set
                {
                    if (value <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                    mustBePositive = value;
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        [Fact]
        public void Setting_Non_Validating_Triggers_Validation()
        {
            var inpcAccessorPlugin = new InpcPropertyAccessorPlugin();
            var validatorPlugin = new ExceptionValidationPlugin();
            var data = new Data();
            var accessor = inpcAccessorPlugin.Start(new WeakReference(data), nameof(data.NonValidated), _ => { });
            IValidationStatus status = null;
            var validator = validatorPlugin.Start(new WeakReference(data), nameof(data.NonValidated), accessor, s => status = s);

            validator.SetValue(5, BindingPriority.LocalValue);

            Assert.NotNull(status);
        }

        [Fact]
        public void Setting_Validating_Property_To_Valid_Value_Returns_Successful_ValidationStatus()
        {
            var inpcAccessorPlugin = new InpcPropertyAccessorPlugin();
            var validatorPlugin = new ExceptionValidationPlugin();
            var data = new Data();
            var accessor = inpcAccessorPlugin.Start(new WeakReference(data), nameof(data.MustBePositive), _ => { });
            IValidationStatus status = null;
            var validator = validatorPlugin.Start(new WeakReference(data), nameof(data.MustBePositive), accessor, s => status = s);

            validator.SetValue(5, BindingPriority.LocalValue);

            Assert.True(status.IsValid);
        }
        
        [Fact]
        public void Setting_Validating_Property_To_Invalid_Value_Returns_Failed_ValidationStatus()
        {
            var inpcAccessorPlugin = new InpcPropertyAccessorPlugin();
            var validatorPlugin = new ExceptionValidationPlugin();
            var data = new Data();
            var accessor = inpcAccessorPlugin.Start(new WeakReference(data), nameof(data.MustBePositive), _ => { });
            IValidationStatus status = null;
            var validator = validatorPlugin.Start(new WeakReference(data), nameof(data.MustBePositive), accessor, s => status = s);

            validator.SetValue(-5, BindingPriority.LocalValue);

            Assert.False(status.IsValid);
        }

        [Fact]
        public void Nulltarget_Dont_Throw_Exception()
        {
            var inpcAccessorPlugin = new InpcPropertyAccessorPlugin();
            Data data = null;
           
            var accessor = inpcAccessorPlugin.Start(new WeakReference(data), nameof(data.MustBePositive), _ => { });

            var ex = ((accessor as PropertyError).Value as BindingError).Exception as MissingMemberException;

            Assert.NotNull(ex);
            Assert.Equal(ex.Message, "Could not find CLR property 'MustBePositive' on ''");
        }

        [Fact]
        public void GCCollectedTarget_Dont_Throw_Exception_OnValueGet()
        {
            var inpcAccessorPlugin = new InpcPropertyAccessorPlugin();
            Data data = new Data() { NonValidated = 1 };

            var accessor = inpcAccessorPlugin.Start(new WeakReference(data), nameof(data.NonValidated), _ => { });

            Assert.Equal(1, (int)accessor.Value);

            var wr = new WeakReference<Data>(data);

            data = null;
            GC.Collect();
            Assert.False(wr.TryGetTarget(out data));
            //data is collected

            object value = accessor.Value;

            Assert.True(value is BindingError);
        }
    }
}
