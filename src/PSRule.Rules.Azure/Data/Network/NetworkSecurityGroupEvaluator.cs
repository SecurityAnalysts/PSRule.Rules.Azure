﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Management.Automation;

namespace PSRule.Rules.Azure.Data.Network
{
    /// <summary>
    /// Evaluates NSG rules to determine resulting access.
    /// </summary>
    public interface INetworkSecurityGroupEvaluator
    {
        Access Outbound(string prefix, int port);
    }

    /// <summary>
    /// The result after evaluatoring a rule.
    /// </summary>
    public enum Access
    {
        Default = 0,

        Allow = 1,

        Deny = 2
    }

    /// <summary>
    /// A basic implementation of an evaluator for checking NSG rules.
    /// </summary>
    internal sealed class NetworkSecurityGroupEvaluator : INetworkSecurityGroupEvaluator
    {
        private const string PROPERTIES = "properties";
        private const string DIRECTION = "direction";
        private const string ACCESS = "access";
        private const string DESTINATION_ADDRESS_PREFIX = "destinationAddressPrefix";
        private const string DESTINATION_ADDRESS_PREFIXES = "destinationAddressPrefixes";
        private const string DESTINATION_PORT_RANGE = "destinationPortRange";
        private const string DESTINATION_PORT_RANGES = "destinationPortRanges";
        private readonly List<SecurityRule> _Outbound;

        internal NetworkSecurityGroupEvaluator()
        {
            _Outbound = new List<SecurityRule>();
        }

        internal enum Direction
        {
            Inbound = 1,

            Outbound = 2
        }

        private sealed class SecurityRule
        {
            public SecurityRule(Direction direction, Access access, string[] destinationAddressPrefixes, string[] destinationPortRanges)
            {
                Direction = direction;
                Access = access;
                DestinationAddressPrefixes = destinationAddressPrefixes == null ? null : new HashSet<string>(destinationAddressPrefixes, StringComparer.OrdinalIgnoreCase);
                DestinationPortRanges = destinationPortRanges == null ? null : new HashSet<string>(destinationPortRanges, StringComparer.OrdinalIgnoreCase);
            }

            public Access Access { get; }

            public Direction Direction { get; }

            public HashSet<string> DestinationAddressPrefixes { get; }

            public HashSet<string> DestinationPortRanges { get; }

            internal bool TryDestinationPrefix(string prefix)
            {
                if (DestinationAddressPrefixes == null)
                    return true;

                return DestinationAddressPrefixes.Contains(prefix);
            }

            internal bool TryDestinationPort(int port)
            {
                if (DestinationPortRanges == null)
                    return true;

                return DestinationPortRanges.Contains(port.ToString());
            }
        }

        public void With(PSObject[] items)
        {
            if (items == null || items.Length == 0)
                return;

            for (var i = 0; i < items.Length; i++)
            {
                var r = GetRule(items[i]);
                if (r.Direction == Direction.Outbound)
                    _Outbound.Add(r);
            }
        }

        public Access Outbound(string prefix, int port)
        {
            for (var i = 0; i < _Outbound.Count; i++)
            {
                if (_Outbound[i].TryDestinationPrefix(prefix) && _Outbound[i].TryDestinationPort(port))
                    return _Outbound[i].Access;
            }
            return Access.Default;
        }

        private static SecurityRule GetRule(PSObject o)
        {
            var properties = o.GetPropertyValue<PSObject>(PROPERTIES);
            var direction = (Direction)Enum.Parse(typeof(Direction), properties.GetPropertyValue<string>(DIRECTION), ignoreCase: true);
            var access = (Access)Enum.Parse(typeof(Access), properties.GetPropertyValue<string>(ACCESS), ignoreCase: true);
            var destinationAddressPrefixes = GetFilter(properties, DESTINATION_ADDRESS_PREFIX) ?? GetFilter(properties, DESTINATION_ADDRESS_PREFIXES);
            var destinationPortRanges = GetFilter(properties, DESTINATION_PORT_RANGE) ?? GetFilter(properties, DESTINATION_PORT_RANGES);

            var result = new SecurityRule(
                direction,
                access,
                destinationAddressPrefixes,
                destinationPortRanges
            );
            return result;
        }

        private static string[] GetFilter(PSObject o, string propertyName)
        {
            if (o.TryProperty(propertyName, out string[] value) && value.Length > 0)
                return value;

            if (o.TryProperty(propertyName, out string s) && s != "*")
                return new string[] { s };

            return null;
        }
    }
}
