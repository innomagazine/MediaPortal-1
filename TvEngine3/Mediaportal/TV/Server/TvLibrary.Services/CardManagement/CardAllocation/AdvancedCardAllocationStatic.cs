#region Copyright (C) 2005-2010 Team MediaPortal

// Copyright (C) 2005-2010 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.
#endregion

using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVService.Interfaces.CardHandler;
using Mediaportal.TV.Server.TVService.Interfaces.Services;

namespace Mediaportal.TV.Server.TVLibrary.CardManagement.CardAllocation
{
  public class AdvancedCardAllocationStatic : AdvancedCardAllocation
  {
    #region overrides

    public override bool CheckTransponder(IUser user, ITvCardHandler tvcard, IChannel tuningDetail)
    {
      return true;
    }

    protected override bool IsSameTransponder(ITvCardHandler tvcard, IChannel tuningDetail)
    {
      return false;
    }

    protected override int NumberOfOtherUsersOnCurrentCard(ITvCardHandler card, IUser user)
    {
      return 0;
    }   

    #endregion
  }
}
