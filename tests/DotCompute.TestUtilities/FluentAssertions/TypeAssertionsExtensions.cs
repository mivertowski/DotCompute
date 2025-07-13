using System;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Types;

namespace DotCompute.TestUtilities.FluentAssertions
{
    /// <summary>
    /// FluentAssertions extensions for Type assertions
    /// Provides BeInterface() and BeClass() methods for type validation
    /// </summary>
    internal static class TypeAssertionsExtensions
    {
        /// <summary>
        /// Asserts that the subject type is an interface.
        /// </summary>
        /// <param name="assertions">The type assertions.</param>
        /// <param name="because">
        /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
        /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
        /// </param>
        /// <param name="becauseArgs">
        /// Zero or more objects to format using the placeholders in <paramref name="because" />.
        /// </param>
        public static AndConstraint<TypeAssertions> BeInterface(this TypeAssertions assertions, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(assertions.Subject?.IsInterface == true)
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected {context:type} to be an interface{reason}, but it is {0}.", 
                    assertions.Subject?.IsInterface == false ? "not an interface" : "null");

            return new AndConstraint<TypeAssertions>(assertions);
        }

        /// <summary>
        /// Asserts that the subject type is a class (not an interface, not a struct, not an enum).
        /// </summary>
        /// <param name="assertions">The type assertions.</param>
        /// <param name="because">
        /// A formatted phrase as is supported by <see cref="string.Format(string,object[])" /> explaining why the assertion
        /// is needed. If the phrase does not start with the word <i>because</i>, it is prepended automatically.
        /// </param>
        /// <param name="becauseArgs">
        /// Zero or more objects to format using the placeholders in <paramref name="because" />.
        /// </param>
        public static AndConstraint<TypeAssertions> BeClass(this TypeAssertions assertions, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .ForCondition(assertions.Subject != null && assertions.Subject.IsClass && !assertions.Subject.IsInterface)
                .BecauseOf(because, becauseArgs)
                .FailWith("Expected {context:type} to be a class{reason}, but it is {0}.", 
                    assertions.Subject == null ? "null" :
                    assertions.Subject.IsInterface ? "an interface" :
                    assertions.Subject.IsEnum ? "an enum" :
                    assertions.Subject.IsValueType ? "a struct" :
                    "not a class");

            return new AndConstraint<TypeAssertions>(assertions);
        }
    }
}