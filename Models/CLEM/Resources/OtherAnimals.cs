﻿using Models.Core;
using Models.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Models.CLEM.Resources
{
    ///<summary>
    /// Parent model of Ruminant Types.
    ///</summary> 
    [Serializable]
    [ViewName("UserInterface.Views.PropertyView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(ResourcesHolder))]
    [Description("This resource group holds all other animals types (not ruminants) for the simulation.")]
    [Version(1, 0, 1, "")]
    [HelpUri(@"Content/Features/Resources/Other animals/OtherAnimals.htm")]
    public class OtherAnimals: ResourceBaseWithTransactions
    {

    }
}
