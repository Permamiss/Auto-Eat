using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace AutoEat
{
    class ModConfig
    {
        public float StaminaThreshold { get; set; } = 0.0f;
        public float HealthThreshold { get; set; } = 0.25f;
    }
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        /*********
        ** Private and public variables
        *********/

        private static bool trueOverexertion = false; //is only set to true when we want the player to become over-exerted for the rest of the in-game day
        private static bool newDay = true; //only true at 6:00 am in-game; used to 
        private static bool eatingFood = false;
        private static bool goodPreviousFrame = false; //used to prevent loss of food when falling to 0 Stamina on the same frame that you receive a Lost Book or something similar, in that order.

        public static bool firstCall = false; //used in clearOldestHUDMessage()
        public static float eatAtAmount_Stamina;
        public static float eatAtAmount_Health;

        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>

        public override void Entry(IModHelper helper)
        {
            ModConfig theConfig = helper.ReadConfig<ModConfig>();
            eatAtAmount_Stamina = theConfig.StaminaThreshold;
            eatAtAmount_Health = theConfig.HealthThreshold;

            if ((eatAtAmount_Stamina < 0.0f) || (eatAtAmount_Health < 0.0f) || (eatAtAmount_Stamina > 1.0f) || (eatAtAmount_Health > 1.0f))
            {                
                ModConfig fixConfig = new ModConfig();
                fixConfig.StaminaThreshold = eatAtAmount_Stamina = 0.0f;
                fixConfig.HealthThreshold = eatAtAmount_Health = 0.25f;
                helper.WriteConfig(fixConfig);
            }
                        
            GameEvents.UpdateTick += this.GameEvents_UpdateTick; //adding the method with the same name below to the corresponding event in order to make them connect
            SaveEvents.BeforeSave += this.SaveEvents_BeforeSave;
            TimeEvents.AfterDayStarted += this.TimeEvents_AfterDayStarted;
        }

        public static void clearOldestHUDMessage() //I may have stolen this idea from CJBok (props to them)
        {
            firstCall = false; //we do this so that, as long as we check for firstCall to be true, this method will not be executed every single tick (if we did not do this, a message would be removed from the HUD every tick!)
            if (Game1.hudMessages.Count > 0) //if there is at least 1 message on the screen, then
                Game1.hudMessages.RemoveAt(Game1.hudMessages.Count - 1); //remove the oldest one (useful in case multiple messages are on the screen at once)
        }

        /// <summary>The method invoked when the player presses a keyboard button.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void GameEvents_UpdateTick(object sender, EventArgs e)
        {
            if (!Context.IsPlayerFree) //are they paused or in a menu or something? then do not continue
            {
                goodPreviousFrame = false;
                return;
            }
            Item cheapestFood = GetCheapestFood();
            if (newDay || (trueOverexertion && !DoesCheapestFoodExist(cheapestFood))) //skip:if it's the beginning of a new day and exhausted without food in inventory
            {
                goodPreviousFrame = false;
                return;
            }

            if ((Game1.player.Stamina <= (eatAtAmount_Stamina * Game1.player.MaxStamina)) || (Game1.player.health <= (eatAtAmount_Health * Game1.player.maxHealth))) //if the player has run out of Energy/Health, then:
            {
                if (!goodPreviousFrame) //makes it so that they have to be "good" (doing nothing, not in a menu) two frames in a row in order for this to pass - necessary thanks to Lost Book bug (tl;dr - wait a frame before continuing)
                {
                    goodPreviousFrame = true;
                    return;
                }
                if (firstCall) //if clearOldestHUDMessage has not been called yet, then
                    clearOldestHUDMessage(); //get rid of the annoying over-exerted message without it noticeably popping up
                if (Game1.player.isEating) //if already eating food, then ignore the rest of the method in order to prevent unnecessary loop                    
                    return;                
                if (cheapestFood != null) //if a cheapest food was found, then:
                {
                    int FoodEdibility = ((StardewValley.Object)cheapestFood).Edibility;
                    int Health_Food = Convert.ToInt32(FoodEdibility * 2.5f * 0.4f);
                    int Stamina_Food = Convert.ToInt32(FoodEdibility * 2.5f);
                    eatingFood = true; //set to true in order to prevent all of this from repeating, as this might cause unwanted side effects (possibly eating multiple foods at once???)
                    Game1.showGlobalMessage("Auto Eat: " + cheapestFood.Name + " - " + Stamina_Food + "s / " + Health_Food + "h"); //makes a message to inform the player of the reason they just stopped what they were doing to be forced to eat a food, lol.             
                    //Game1.player.eatObject((StardewValley.Object)cheapestFood); //cast the cheapestFood Item to be an Object since playerEatObject only accepts Objects, finally allowing the player to eat the cheapest food they have on them.
                    //Manually add to players health and stamina to prevent eating animation, this caused issues when in mining in the caves, major lag that is
                    Game1.player.health += Health_Food;
                    if (Game1.player.health > Game1.player.maxHealth)
                        Game1.player.health = Game1.player.maxHealth;
                    Game1.player.stamina += Stamina_Food;
                    if (Game1.player.stamina > Game1.player.MaxStamina)
                        Game1.player.stamina = Game1.player.MaxStamina;
                    //Game1.playerEatObject((StardewValley.Object)cheapestFood); //<== pre-multiplayer beta version of above line of code.
                    cheapestFood.Stack--; //stack being the amount of the cheapestFood that the player has on them, we have to manually decrement this apparently, as playerEatObject does not do this itself for some reason.
                    if (cheapestFood.Stack == 0) //if the stack has hit the number 0, then
                        Game1.player.removeItemFromInventory(cheapestFood); //delete the item from the player's inventory..I don't want to know what would happen if they tried to use it when it was at 0!
                }
                else
                {
                    if (Game1.player.stamina <= 0)  //set exhausted flag only if stamina is gone
                    {
                        trueOverexertion = true;
                    }
                }                                
            }
            else //if they have Energy (whether it's gained from food or it's the start of a day or whatever), then:
            {
                goodPreviousFrame = false;
                firstCall = true; //we set this to true here so that "clearOldestHUDMessage()" can seamlessly remove the "over-exerted" message whenever it needs to
                if (eatingFood) //if the player was eating food before, then:
                {
                    eatingFood = false; //they are no longer eating, meaning the above checks will be performed once more if they hit 0 Energy again.
                    Game1.player.exhausted = false; //forcing the game to make the player not over-exerted anymore since that's what this mod's goal was
                    Game1.player.checkForExhaustion(Game1.player.Stamina); //forcing the game to make the player not over-exerted anymore since that's what this mod's goal was
                    trueOverexertion = false; //fix exhaustion flag, after all we did just eat
                }
            }
        }

        //will return null if no item found
        private Item GetCheapestFood() 
        {
            Item cheapestFood = null; //currently set to "null" (aka none), as we have not found a food yet
            foreach (Item curItem in Game1.player.Items) //check all of the player's inventory items sequentially (with "curItem" meaning "current item") for the following:
            {
                if (curItem is StardewValley.Object && ((StardewValley.Object)curItem).Edibility > 0) //is it an Object (rather than, say, a Tool), and is it a food with positive Edibility (aka Energy)? then,
                {
                    if (cheapestFood == null) //if we do not yet have a cheapest food set, then
                        cheapestFood = curItem; //the cheapest food has to be the current item, so that we can compare its price to another item without getting errors
                    else if (((StardewValley.Object)curItem).Edibility < ((StardewValley.Object)cheapestFood).Edibility) //lowest edibility should be lowest cost, as health and stamina are calculated off this base
                    //else if ((curItem.salePrice() / ((StardewValley.Object)curItem).Edibility) < (cheapestFood.salePrice() / ((StardewValley.Object)cheapestFood).Edibility)) //however, if we already have a cheapest food, and the price of the current item is even less, then
                        cheapestFood = curItem; //the cheapest food we have is actually the current item!
                }                
            }
            return cheapestFood;
        }

        //check against GetCheapestFood item returned
        private Boolean DoesCheapestFoodExist(Item cheapestFood)
        {
            if (cheapestFood != null)
            {
                return true;
            } else
            {
                return false;
            }
        }

        private void SaveEvents_BeforeSave(object sender, EventArgs e)
        {
            newDay = true;
        }

        private void TimeEvents_AfterDayStarted(object sender, EventArgs e) //fires just as player wakes up
        {
            newDay = false; //reset the variable, allowing the UpdateTick method checks to occur once more
            trueOverexertion = false; //reset the variable, allowing the UpdateTick method checks to occur once more (in other words, allowing the player to avoid over-exertion once more)
            eatingFood = false; //reset the variable (this one isn't necessary as far as I know, but who knows? maybe a person will run out of stamina right as they hit 2:00 am in-game.)
        }
    }
}
