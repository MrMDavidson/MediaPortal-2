﻿#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using MediaPortal.Common;
using MediaPortal.Common.PathManager;
using MediaPortal.Common.Settings;
using MediaPortal.UiComponents.Weather.Settings;

namespace MediaPortal.UiComponents.Weather.Grabbers
{
  public class WorldWeatherOnlineCatcher : IWeatherCatcher
  {
    public const string SERVICE_NAME = "WorldWeatherOnline.com";

    private const string CELCIUS = " °C";
    private const string FAHRENHEIT = " °F";
    private const string KPH = " km/h";
    private const string MPH = " mph";
    private const string PERCENT = " %";
    private const string MM = " mm";

    private readonly TimeSpan _maxCacheDuration = TimeSpan.FromHours(1);

    private readonly bool _preferCelcius = true;
    private readonly bool _preferKph = true;
    private readonly string _parsefileLocation;

    private readonly Dictionary<int, int> _weatherCodeTranslation = new Dictionary<int, int>();

    public WorldWeatherOnlineCatcher()
    {
      WeatherSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<WeatherSettings>();
      _preferCelcius = settings.TemperatureUnit == WeatherSettings.TempUnit.Celcius;
      _preferKph = settings.WindSpeedUnit == WeatherSettings.SpeedUnit.Kph;
      _parsefileLocation = ServiceRegistration.Get<IPathManager>().GetPath(settings.ParsefileLocation);

      #region Weather code translation

      _weatherCodeTranslation[395] = 42; //  Moderate or heavy snow in area with thunder
      _weatherCodeTranslation[392] = 14; //  Patchy light snow in area with thunder
      _weatherCodeTranslation[389] = 40; //  Moderate or heavy rain in area with thunder
      _weatherCodeTranslation[386] = 3; //  Patchy light rain in area with thunder
      _weatherCodeTranslation[377] = 18; //  Moderate or heavy showers of ice pellets
      _weatherCodeTranslation[374] = 18; //  Light showers of ice pellets
      _weatherCodeTranslation[371] = 16; //  Moderate or heavy snow showers
      _weatherCodeTranslation[368] = 14; //  Light snow showers
      _weatherCodeTranslation[365] = 6; //  Moderate or heavy sleet showers
      _weatherCodeTranslation[362] = 6; //  Light sleet showers
      _weatherCodeTranslation[359] = 12; //  Torrential rain shower
      _weatherCodeTranslation[356] = 40; //  Moderate or heavy rain shower
      _weatherCodeTranslation[353] = 39; //  Light rain shower
      _weatherCodeTranslation[350] = 18; //  Ice pellets
      _weatherCodeTranslation[338] = 42; //  Heavy snow
      _weatherCodeTranslation[335] = 16; //  Patchy heavy snow
      _weatherCodeTranslation[332] = 41; //  Moderate snow
      _weatherCodeTranslation[329] = 14; //  Patchy moderate snow
      _weatherCodeTranslation[326] = 14; //  Light snow
      _weatherCodeTranslation[323] = 14; //  Patchy light snow
      _weatherCodeTranslation[320] = 6; //  Moderate or heavy sleet
      _weatherCodeTranslation[317] = 6; //  Light sleet
      _weatherCodeTranslation[314] = 10; //  Moderate or Heavy freezing rain
      _weatherCodeTranslation[311] = 10; //  Light freezing rain
      _weatherCodeTranslation[308] = 40; //  Heavy rain
      _weatherCodeTranslation[305] = 39; //  Heavy rain at times
      _weatherCodeTranslation[302] = 40; //  Moderate rain
      _weatherCodeTranslation[299] = 39; //  Moderate rain at times
      _weatherCodeTranslation[296] = 11; //  Light rain
      _weatherCodeTranslation[293] = 11; //  Patchy light rain
      _weatherCodeTranslation[284] = 8; //  Heavy freezing drizzle
      _weatherCodeTranslation[281] = 8; //  Freezing drizzle
      _weatherCodeTranslation[266] = 9; //  Light drizzle
      _weatherCodeTranslation[263] = 9; //  Patchy light drizzle
      _weatherCodeTranslation[260] = 20; //  Freezing fog
      _weatherCodeTranslation[248] = 20; //  Fog
      _weatherCodeTranslation[230] = 42; //  Blizzard
      _weatherCodeTranslation[227] = 43; //  Blowing snow
      _weatherCodeTranslation[200] = 35; //  Thundery outbreaks in nearby
      _weatherCodeTranslation[185] = 8; //  Patchy freezing drizzle nearby
      _weatherCodeTranslation[182] = 6; //  Patchy sleet nearby
      _weatherCodeTranslation[179] = 41; //  Patchy snow nearby
      _weatherCodeTranslation[176] = 39; //  Patchy rain nearby
      _weatherCodeTranslation[143] = 20; //  Mist
      _weatherCodeTranslation[122] = 26; //  Overcast
      _weatherCodeTranslation[119] = 26; //  Cloudy
      _weatherCodeTranslation[116] = 30; //  Partly Cloudy
      _weatherCodeTranslation[113] = 32; //  Clear/Sunny

      #endregion
    }

    #region IWeatherCatcher implementation

    public bool GetLocationData(City city)
    {
      string locationKey = city.Id;
      if (string.IsNullOrEmpty(locationKey))
        return false;

      city.HasData = false;
      string cachefile = string.Format(_parsefileLocation, locationKey.Replace(',', '_'));

      XPathDocument doc;
      if (ShouldUseCache(cachefile))
      {
        doc = new XPathDocument(cachefile);
      }
      else
      {
        Dictionary<string, string> args = new Dictionary<string, string>();
        args["q"] = city.Id;
        args["num_of_days"] = "5";

        string url = BuildRequest("weather.ashx", args);
        doc = WorldWeatherOnlineHelper.GetOnlineContent(url);

        // Save cache file
        using (XmlWriter xw = XmlWriter.Create(cachefile))
        {
          doc.CreateNavigator().WriteSubtree(xw);
          xw.Close();
        }
      }
      return Parse(city, doc);
    }

    /// <summary>
    /// Checks requirements for using local cache file instead of requesting new data from web.
    /// </summary>
    /// <param name="cachefile">Filename</param>
    /// <returns>True if cache is valid and should be used.</returns>
    private bool ShouldUseCache(string cachefile)
    {
      FileInfo fileInfo = new FileInfo(cachefile);
      return fileInfo.Exists && DateTime.Now - fileInfo.LastWriteTime <= _maxCacheDuration;
    }

    /// <summary>
    /// Returns a new List of City objects if the searched
    /// location was found... the unique id (City.Id) and the
    /// location name will be set for each City...
    /// </summary>
    /// <param name="locationName">Name of the location to search for</param>
    /// <returns>New City List</returns>
    public List<CitySetupInfo> FindLocationsByName(string locationName)
    {
      // create list that will hold all found cities
      List<CitySetupInfo> locations = new List<CitySetupInfo>();

      Dictionary<string, string> args = new Dictionary<string, string>();
      args["query"] = locationName;
      args["num_of_results"] = "5";

      string url = BuildRequest("search.ashx", args);
      XPathDocument doc = WorldWeatherOnlineHelper.GetOnlineContent(url);
      XPathNavigator navigator = doc.CreateNavigator();
      XPathNodeIterator nodes = navigator.Select("/search_api/result");

      while (nodes.MoveNext())
      {
        CitySetupInfo city = new CitySetupInfo();
        XPathNavigator nodesNavigator = nodes.Current;
        if (nodesNavigator == null)
          continue;

        XPathNavigator areaNode = nodesNavigator.SelectSingleNode("areaName");
        if (areaNode != null)
          city.Name = areaNode.Value;

        XPathNavigator countryNode = nodesNavigator.SelectSingleNode("country");
        if (countryNode != null)
          city.Name += " (" + countryNode.Value + ")";

        // Build a unique key based on latitude & longitude
        XPathNavigator latNode = nodesNavigator.SelectSingleNode("latitude");
        XPathNavigator lonNode = nodesNavigator.SelectSingleNode("longitude");
        if (latNode != null && lonNode != null)
          city.Id = latNode.Value + "," + lonNode.Value;

        city.Grabber = GetServiceName();
        locations.Add(city);
      }
      return locations;
    }

    public string GetServiceName()
    {
      return SERVICE_NAME;
    }

    private bool Parse(City city, XPathDocument doc)
    {
      if (city == null || doc == null)
        return false;
      XPathNavigator navigator = doc.CreateNavigator();
      XPathNavigator condition = navigator.SelectSingleNode("/data/current_condition");
      if (condition != null)
      {
        string nodeName = _preferCelcius ? "temp_C" : "temp_F";
        string unit = _preferCelcius ? CELCIUS : FAHRENHEIT;

        XPathNavigator node = condition.SelectSingleNode(nodeName);
        if (node != null)
          city.Condition.Temperature = node.Value + unit;

        nodeName = _preferKph ? "windspeedKmph" : "windspeedMiles";
        unit = _preferKph ? KPH : MPH;
        node = condition.SelectSingleNode(nodeName);
        if (node != null)
          city.Condition.Wind = node.Value + unit;

        node = condition.SelectSingleNode("winddir16Point");
        if (node != null)
          city.Condition.Wind += " " + node.Value;

        node = condition.SelectSingleNode("humidity");
        if (node != null)
          city.Condition.Humidity = node.Value + PERCENT;

        node = condition.SelectSingleNode("weatherDesc");
        if (node != null)
          city.Condition.Condition = node.Value;

        node = condition.SelectSingleNode("weatherCode");
        if (node != null)
        {
          city.Condition.BigIcon = @"Weather\128x128\" + GetWeatherIcon(node.ValueAsInt);
          city.Condition.SmallIcon = @"Weather\64x64\" + GetWeatherIcon(node.ValueAsInt);
        }
      }

      XPathNodeIterator forecasts = navigator.Select("/data/weather");
      city.ForecastCollection.Clear();
      while (forecasts.MoveNext())
      {
        if (forecasts.Current == null)
          continue;

        DayForecast dayForecast = new DayForecast();

        XPathNavigator node = forecasts.Current.SelectSingleNode("date");
        if (node != null)
          dayForecast.Day = node.Value;

        string nodeName = _preferCelcius ? "tempMinC" : "tempMinF";
        string unit = _preferCelcius ? CELCIUS : FAHRENHEIT;

        node = forecasts.Current.SelectSingleNode(nodeName);
        if (node != null)
          dayForecast.Low = node.Value + unit;

        nodeName = _preferCelcius ? "tempMaxC" : "tempMaxF";
        node = forecasts.Current.SelectSingleNode(nodeName);
        if (node != null)
          dayForecast.High = node.Value + unit;

        nodeName = _preferKph ? "windspeedKmph" : "windspeedMiles";
        unit = _preferKph ? KPH : MPH;
        node = forecasts.Current.SelectSingleNode(nodeName);
        if (node != null)
          dayForecast.Wind = node.Value + unit;

        node = forecasts.Current.SelectSingleNode("winddir16Point");
        if (node != null)
          dayForecast.Wind += " " + node.Value;

        node = forecasts.Current.SelectSingleNode("weatherCode");
        if (node != null)
        {
          dayForecast.BigIcon = @"Weather\128x128\" + GetWeatherIcon(node.ValueAsInt);
          dayForecast.SmallIcon = @"Weather\64x64\" + GetWeatherIcon(node.ValueAsInt);
        }

        node = forecasts.Current.SelectSingleNode("weatherDesc");
        if (node != null)
          dayForecast.Overview = node.Value;

        node = forecasts.Current.SelectSingleNode("precipMM");
        if (node != null)
          dayForecast.Precipitation = node.Value + MM;

        city.ForecastCollection.Add(dayForecast);
      }
      return true;
    }

    private string GetWeatherIcon(int weatherCode)
    {
      int translatedID;
      if (_weatherCodeTranslation.TryGetValue(weatherCode, out translatedID))
        return translatedID + ".png";
      return "na.png";
    }

    private static string BuildRequest(string relativePage, Dictionary<string, string> args)
    {
      return string.Format("http://free.worldweatheronline.com/feed/{0}?{1}", relativePage, BuildRequestArgs(args));
    }

    private static string BuildRequestArgs(Dictionary<string, string> args)
    {
      StringBuilder requestArgs = new StringBuilder();
      foreach (KeyValuePair<string, string> keyValuePair in args)
      {
        if (requestArgs.Length > 0)
          requestArgs.Append("&");
        requestArgs.Append(string.Format("{0}={1}", keyValuePair.Key, Uri.EscapeUriString(keyValuePair.Value)));
      }
      return requestArgs.ToString();
    }

    #endregion
  }
}