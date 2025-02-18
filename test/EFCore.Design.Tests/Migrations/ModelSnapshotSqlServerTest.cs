﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable ClassNeverInstantiated.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedTypeParameter
namespace Microsoft.EntityFrameworkCore.Migrations
{
    public class ModelSnapshotSqlServerTest
    {
        private class EntityWithManyProperties
        {
            public int Id { get; set; }
            public string String { get; set; }
            public byte[] Bytes { get; set; }
            public short Int16 { get; set; }
            public int Int32 { get; set; }
            public long Int64 { get; set; }
            public double Double { get; set; }
            public decimal Decimal { get; set; }
            public DateTime DateTime { get; set; }
            public DateTimeOffset DateTimeOffset { get; set; }
            public TimeSpan TimeSpan { get; set; }
            public float Single { get; set; }
            public bool Boolean { get; set; }
            public byte Byte { get; set; }
            public ushort UnsignedInt16 { get; set; }
            public uint UnsignedInt32 { get; set; }
            public ulong UnsignedInt64 { get; set; }
            public char Character { get; set; }
            public sbyte SignedByte { get; set; }
            public Enum64 Enum64 { get; set; }
            public Enum32 Enum32 { get; set; }
            public Enum16 Enum16 { get; set; }
            public Enum8 Enum8 { get; set; }
            public EnumU64 EnumU64 { get; set; }
            public EnumU32 EnumU32 { get; set; }
            public EnumU16 EnumU16 { get; set; }
            public EnumS8 EnumS8 { get; set; }
            public Geometry SpatialBGeometryCollection { get; set; }
            public Geometry SpatialBLineString { get; set; }
            public Geometry SpatialBMultiLineString { get; set; }
            public Geometry SpatialBMultiPoint { get; set; }
            public Geometry SpatialBMultiPolygon { get; set; }
            public Geometry SpatialBPoint { get; set; }
            public Geometry SpatialBPolygon { get; set; }
            public GeometryCollection SpatialCGeometryCollection { get; set; }
            public LineString SpatialCLineString { get; set; }
            public MultiLineString SpatialCMultiLineString { get; set; }
            public MultiPoint SpatialCMultiPoint { get; set; }
            public MultiPolygon SpatialCMultiPolygon { get; set; }
            public Point SpatialCPoint { get; set; }
            public Polygon SpatialCPolygon { get; set; }
        }

        private enum Enum64 : long
        {
            SomeValue = 1
        }

        private enum Enum32
        {
            SomeValue = 1
        }

        private enum Enum16 : short
        {
            SomeValue = 1
        }

        private enum Enum8 : byte
        {
            SomeValue = 1
        }

        private enum EnumU64 : ulong
        {
            SomeValue = 1234567890123456789UL
        }

        private enum EnumU32 : uint
        {
            SomeValue = uint.MaxValue
        }

        private enum EnumU16 : ushort
        {
            SomeValue = ushort.MaxValue
        }

        private enum EnumS8 : sbyte
        {
            SomeValue = sbyte.MinValue
        }

        private class EntityWithOneProperty
        {
            public int Id { get; set; }
            public EntityWithTwoProperties EntityWithTwoProperties { get; set; }
        }

        private class EntityWithTwoProperties
        {
            public int Id { get; set; }
            public int AlternateId { get; set; }
            public EntityWithOneProperty EntityWithOneProperty { get; set; }

            [NotMapped]
            public EntityWithStringKey EntityWithStringKey { get; set; }
        }

        private class EntityWithStringProperty
        {
            public int Id { get; set; }
            public string Name { get; set; }

            [NotMapped]
            public EntityWithOneProperty EntityWithOneProperty { get; set; }
        }

        private class EntityWithStringKey
        {
            public string Id { get; set; }
            public ICollection<EntityWithStringProperty> Properties { get; set; }
        }

        private class EntityWithGenericKey<TKey>
        {
            public Guid Id { get; set; }
        }

        private class EntityWithGenericProperty<TProperty>
        {
            public int Id { get; set; }
            public TProperty Property { get; set; }
        }

        private class BaseEntity
        {
            public int Id { get; set; }

            public string Discriminator { get; set; }
        }

        private class DerivedEntity : BaseEntity
        {
            public string Name { get; set; }
        }

        private class AnotherDerivedEntity : BaseEntity
        {
            public string Title { get; set; }
        }

        private class BaseType
        {
            public int Id { get; set; }

            public EntityWithOneProperty Navigation { get; set; }
        }

        private class DerivedType : BaseType
        {
        }

        private enum Days : long
        {
            Sun,
            Mon,
            Tue,
            Wed,
            Thu,
            Fri,
            Sat
        }

        private class EntityWithEnumType
        {
            public int Id { get; set; }
            public Days Day { get; set; }
        }

        private class EntityWithNullableEnumType
        {
            public int Id { get; set; }
            public Days? Day { get; set; }
        }

        private class CustomValueGenerator : ValueGenerator<int>
        {
            public override int Next(EntityEntry entry) => throw new NotImplementedException();

            public override bool GeneratesTemporaryValues => false;
        }

        #region Model

        [ConditionalFact]
        public virtual void Model_annotations_are_stored_in_snapshot()
        {
            Test(
                builder => builder.HasAnnotation("AnnotationName", "AnnotationValue"),
                AddBoilerPlate(
                    @"
            modelBuilder
                .HasAnnotation(""AnnotationName"", ""AnnotationValue"")
                .HasAnnotation(""Relational:MaxIdentifierLength"", 128)
                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);"),
                o =>
                {
                    Assert.Equal(3, o.GetAnnotations().Count());
                    Assert.Equal("AnnotationValue", o["AnnotationName"]);
                });
        }

        [ConditionalFact]
        public virtual void Model_default_schema_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.HasDefaultSchema("DefaultSchema");
                    builder.HasAnnotation("AnnotationName", "AnnotationValue");
                },
                AddBoilerPlate(
                    @"
            modelBuilder
                .HasDefaultSchema(""DefaultSchema"")
                .HasAnnotation(""AnnotationName"", ""AnnotationValue"")
                .HasAnnotation(""Relational:MaxIdentifierLength"", 128)
                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);"),
                o =>
                {
                    Assert.Equal(4, o.GetAnnotations().Count());
                    Assert.Equal("AnnotationValue", o["AnnotationName"]);
                    Assert.Equal("DefaultSchema", o[RelationalAnnotationNames.DefaultSchema]);
                });
        }

        [ConditionalFact]
        public virtual void Entities_are_stored_in_model_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>().Ignore(e => e.EntityWithTwoProperties);
                    builder.Entity<EntityWithTwoProperties>().Ignore(e => e.EntityWithOneProperty);
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    Assert.Equal(2, o.GetEntityTypes().Count());
                    Assert.Collection(
                        o.GetEntityTypes(),
                        t => Assert.Equal(
                            "Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty", t.Name),
                        t => Assert.Equal(
                            "Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties", t.Name));
                });
        }

        [ConditionalFact]
        public virtual void Sequence_is_stored_in_snapshot_as_annotations()
        {
            Test(
                builder =>
                {
                    builder.HasSequence<int>("Foo", "Bar")
                        .StartsAt(2)
                        .HasMin(1)
                        .HasMax(3)
                        .IncrementsBy(2)
                        .IsCyclic();
                },
                AddBoilerPlate(
                    @"
            modelBuilder
                .HasAnnotation(""Relational:MaxIdentifierLength"", 128)
                .HasAnnotation(""Relational:Sequence:Bar.Foo"", ""'Foo', 'Bar', '2', '2', '1', '3', 'Int32', 'True'"")
                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);"),
                o =>
                {
                    Assert.Equal(3, o.GetAnnotations().Count());
                });
        }

        [ConditionalFact(Skip = "Issue #14103")]
        public virtual void Sequence_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.HasSequence<int>("Foo", "Bar")
                        .StartsAt(2)
                        .HasMin(1)
                        .HasMax(3)
                        .IncrementsBy(2)
                        .IsCyclic();
                },
                AddBoilerPlate(
                    @"
            modelBuilder
                .HasAnnotation(""Relational:MaxIdentifierLength"", 128)
                .HasSequence<int>(""Foo"", ""Bar"")
                        .StartsAt(2)
                        .HasMin(1)
                        .HasMax(3)
                        .IncrementsBy(2)
                        .IsCyclic();"),
                o =>
                {
                    Assert.Equal(3, o.GetAnnotations().Count());
                });
        }

        [ConditionalFact]
        public virtual void CheckConstraint_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>()
                        .HasCheckConstraint("CK_Customer_AlternateId", "AlternateId > Id");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");

                    b.HasCheckConstraint(""CK_Customer_AlternateId"", ""AlternateId > Id"");
                });"),
                o =>
                {
                    Assert.Equal(2, o.GetAnnotations().Count());
                });
        }

        [ConditionalFact]
        public virtual void CheckConstraint_is_only_stored_in_snapshot_once_for_TPH()
        {
            Test(
                builder =>
                {
                    builder.Entity<DerivedEntity>()
                        .HasBaseType<BaseEntity>()
                        .HasCheckConstraint("CK_Customer_AlternateId", "AlternateId > Id");
                    builder.Entity<BaseEntity>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Discriminator"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(max)"");

                    b.HasKey(""Id"");

                    b.ToTable(""BaseEntity"");

                    b.HasDiscriminator<string>(""Discriminator"").HasValue(""BaseEntity"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+DerivedEntity"", b =>
                {
                    b.HasBaseType(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"");

                    b.Property<string>(""Name"")
                        .HasColumnType(""nvarchar(max)"");

                    b.HasDiscriminator().HasValue(""DerivedEntity"");

                    b.HasCheckConstraint(""CK_Customer_AlternateId"", ""AlternateId > Id"");
                });"),
                o =>
                {
                    Assert.Equal(2, o.GetAnnotations().Count());
                });
        }

        #endregion

        #region EntityType

        [ConditionalFact]
        public virtual void EntityType_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>().HasAnnotation("AnnotationName", "AnnotationValue");
                    builder.Ignore<EntityWithTwoProperties>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");

                    b.HasAnnotation(""AnnotationName"", ""AnnotationValue"");
                });"),
                o =>
                {
                    Assert.Equal(2, o.GetEntityTypes().First().GetAnnotations().Count());
                    Assert.Equal("AnnotationValue", o.GetEntityTypes().First()["AnnotationName"]);
                });
        }

        [ConditionalFact]
        public virtual void BaseType_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<DerivedEntity>().HasBaseType<BaseEntity>();
                    builder.Entity<AnotherDerivedEntity>().HasBaseType<BaseEntity>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Discriminator"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(max)"");

                    b.HasKey(""Id"");

                    b.ToTable(""BaseEntity"");

                    b.HasDiscriminator<string>(""Discriminator"").HasValue(""BaseEntity"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+AnotherDerivedEntity"", b =>
                {
                    b.HasBaseType(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"");

                    b.Property<string>(""Title"")
                        .HasColumnType(""nvarchar(max)"");

                    b.HasDiscriminator().HasValue(""AnotherDerivedEntity"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+DerivedEntity"", b =>
                {
                    b.HasBaseType(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"");

                    b.Property<string>(""Name"")
                        .HasColumnType(""nvarchar(max)"");

                    b.HasDiscriminator().HasValue(""DerivedEntity"");
                });"),
                o =>
                {
                    Assert.Equal(3, o.GetEntityTypes().Count());
                    Assert.Collection(
                        o.GetEntityTypes(),
                        t => Assert.Equal(
                            "Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+AnotherDerivedEntity", t.Name),
                        t => Assert.Equal("Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity", t.Name),
                        t => Assert.Equal("Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+DerivedEntity", t.Name)
                    );
                });
        }

        [ConditionalFact]
        public virtual void Discriminator_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<DerivedEntity>().HasBaseType<BaseEntity>();
                    builder.Entity<AnotherDerivedEntity>().HasBaseType<BaseEntity>();
                    builder.Entity<BaseEntity>()
                        .HasDiscriminator(e => e.Discriminator)
                        .HasValue(typeof(BaseEntity), typeof(BaseEntity).Name)
                        .HasValue(typeof(DerivedEntity), typeof(DerivedEntity).Name)
                        .HasValue(typeof(AnotherDerivedEntity), typeof(AnotherDerivedEntity).Name);
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Discriminator"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(max)"");

                    b.HasKey(""Id"");

                    b.ToTable(""BaseEntity"");

                    b.HasDiscriminator<string>(""Discriminator"").HasValue(""BaseEntity"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+AnotherDerivedEntity"", b =>
                {
                    b.HasBaseType(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"");

                    b.Property<string>(""Title"")
                        .HasColumnType(""nvarchar(max)"");

                    b.HasDiscriminator().HasValue(""AnotherDerivedEntity"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+DerivedEntity"", b =>
                {
                    b.HasBaseType(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseEntity"");

                    b.Property<string>(""Name"")
                        .HasColumnType(""nvarchar(max)"");

                    b.HasDiscriminator().HasValue(""DerivedEntity"");
                });"),
                o =>
                {
                    Assert.Equal("Discriminator", o.FindEntityType(typeof(BaseEntity))[CoreAnnotationNames.DiscriminatorProperty]);
                    Assert.Equal("BaseEntity", o.FindEntityType(typeof(BaseEntity))[CoreAnnotationNames.DiscriminatorValue]);
                    Assert.Equal(
                        "AnotherDerivedEntity",
                        o.FindEntityType(typeof(AnotherDerivedEntity))[CoreAnnotationNames.DiscriminatorValue]);
                    Assert.Equal("DerivedEntity", o.FindEntityType(typeof(DerivedEntity))[CoreAnnotationNames.DiscriminatorValue]);
                });
        }

        [ConditionalFact]
        public virtual void Properties_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>();
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    Assert.Equal(2, o.GetEntityTypes().First().GetProperties().Count());
                    Assert.Collection(
                        o.GetEntityTypes().First().GetProperties(),
                        t => Assert.Equal("Id", t.Name),
                        t => Assert.Equal("AlternateId", t.Name)
                    );
                });
        }

        [ConditionalFact]
        public virtual void Primary_key_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasKey(
                        t => new
                        {
                            t.Id,
                            t.AlternateId
                        });
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .HasColumnType(""int"");

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"", ""AlternateId"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    Assert.Equal(2, o.GetEntityTypes().First().FindPrimaryKey().Properties.Count);
                    Assert.Collection(
                        o.GetEntityTypes().First().FindPrimaryKey().Properties,
                        t => Assert.Equal("Id", t.Name),
                        t => Assert.Equal("AlternateId", t.Name)
                    );
                });
        }

        [ConditionalFact]
        public virtual void Alternate_keys_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasAlternateKey(
                        t => new
                        {
                            t.Id,
                            t.AlternateId
                        });
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasAlternateKey(""Id"", ""AlternateId"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    Assert.Collection(
                        o.GetEntityTypes().First().GetDeclaredKeys().First(k => k.Properties.Count == 2).Properties,
                        t => Assert.Equal("Id", t.Name),
                        t => Assert.Equal("AlternateId", t.Name)
                    );
                });
        }

        [ConditionalFact]
        public virtual void Indexes_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasIndex(t => t.AlternateId);
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    Assert.Equal(1, o.GetEntityTypes().First().GetIndexes().Count());
                    Assert.Equal("AlternateId", o.GetEntityTypes().First().GetIndexes().First().Properties[0].Name);
                });
        }

        [ConditionalFact]
        public virtual void Indexes_are_stored_in_snapshot_including_composite_index()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasIndex(
                        t => new
                        {
                            t.Id,
                            t.AlternateId
                        });
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""Id"", ""AlternateId"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    Assert.Equal(1, o.GetEntityTypes().First().GetIndexes().Count());
                    Assert.Collection(
                        o.GetEntityTypes().First().GetIndexes().First().Properties,
                        t => Assert.Equal("Id", t.Name),
                        t => Assert.Equal("AlternateId", t.Name));
                });
        }

        [ConditionalFact]
        public virtual void Foreign_keys_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder
                        .Entity<EntityWithTwoProperties>()
                        .HasOne(e => e.EntityWithOneProperty)
                        .WithOne(e => e.EntityWithTwoProperties)
                        .HasForeignKey<EntityWithTwoProperties>(e => e.AlternateId);
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .IsUnique();

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""EntityWithOneProperty"")
                        .WithOne(""EntityWithTwoProperties"")
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""AlternateId"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o =>
                {
                    var foreignKey = o.FindEntityType(typeof(EntityWithTwoProperties)).GetForeignKeys().Single();
                    Assert.Equal("AlternateId", foreignKey.Properties[0].Name);
                    Assert.Equal("EntityWithTwoProperties", foreignKey.PrincipalToDependent.Name);
                    Assert.Equal("EntityWithOneProperty", foreignKey.DependentToPrincipal.Name);
                });
        }

        [ConditionalFact]
        public virtual void TableName_preserved_when_generic()
        {
            IModel originalModel = null;

            Test(
                builder =>
                {
                    builder.Entity<EntityWithGenericKey<Guid>>();

                    originalModel = builder.Model;
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithGenericKey<System.Guid>"", b =>
                {
                    b.Property<Guid>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""uniqueidentifier"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithGenericKey<Guid>"");
                });", usingSystem: true),
                model =>
                {
                    var originalEntity = originalModel.FindEntityType(typeof(EntityWithGenericKey<Guid>));
                    var entity = model.FindEntityType(originalEntity.Name);

                    Assert.NotNull(entity);
                    Assert.Equal(originalEntity.GetTableName(), entity.GetTableName());
                });
        }

        [ConditionalFact]
        public virtual void PrimaryKey_name_preserved_when_generic()
        {
            IModel originalModel = null;

            Test(
                builder =>
                {
                    builder.Entity<EntityWithGenericKey<Guid>>();

                    originalModel = builder.Model;
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithGenericKey<System.Guid>"", b =>
                {
                    b.Property<Guid>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""uniqueidentifier"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithGenericKey<Guid>"");
                });", usingSystem: true),
                model =>
                {
                    var originalEntity = originalModel.FindEntityType(typeof(EntityWithGenericKey<Guid>));
                    var entity = model.FindEntityType(originalEntity.Name);
                    Assert.NotNull(entity);

                    var originalPrimaryKey = originalEntity.FindPrimaryKey();
                    var primaryKey = entity.FindPrimaryKey();

                    Assert.Equal(originalPrimaryKey.GetName(), primaryKey.GetName());
                });
        }

        [ConditionalFact]
        public virtual void AlternateKey_name_preserved_when_generic()
        {
            IModel originalModel = null;

            Test(
                builder =>
                {
                    builder.Entity<EntityWithGenericProperty<Guid>>().HasAlternateKey(e => e.Property);

                    originalModel = builder.Model;
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithGenericProperty<System.Guid>"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<Guid>(""Property"")
                        .HasColumnType(""uniqueidentifier"");

                    b.HasKey(""Id"");

                    b.HasAlternateKey(""Property"");

                    b.ToTable(""EntityWithGenericProperty<Guid>"");
                });", usingSystem: true),
                model =>
                {
                    var originalEntity = originalModel.FindEntityType(typeof(EntityWithGenericProperty<Guid>));
                    var entity = model.FindEntityType(originalEntity.Name);
                    Assert.NotNull(entity);

                    var originalAlternateKey = originalEntity.FindKey(originalEntity.FindProperty("Property"));
                    var alternateKey = entity.FindKey(entity.FindProperty("Property"));

                    Assert.Equal(originalAlternateKey.GetName(), alternateKey.GetName());
                });
        }

        [ConditionalFact]
        public virtual void Discriminator_of_enum()
        {
            Test(
                builder => builder.Entity<EntityWithEnumType>().HasDiscriminator(e => e.Day),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithEnumType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<long>(""Day"")
                        .HasColumnType(""bigint"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithEnumType"");

                    b.HasDiscriminator<long>(""Day"");
                });"),
                model => Assert.Equal(typeof(long), model.GetEntityTypes().First().GetDiscriminatorProperty().ClrType));
        }

        [ConditionalFact]
        public virtual void Discriminator_of_enum_to_string()
        {
            Test(
                builder => builder.Entity<EntityWithEnumType>(
                    x =>
                    {
                        x.Property(e => e.Day).HasConversion<string>();
                        x.HasDiscriminator(e => e.Day);
                    }),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithEnumType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Day"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(max)"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithEnumType"");

                    b.HasDiscriminator<string>(""Day"");
                });"),
                model =>
                {
                    var discriminatorProperty = model.GetEntityTypes().First().GetDiscriminatorProperty();
                    Assert.Equal(typeof(string), discriminatorProperty.ClrType);
                    Assert.False(discriminatorProperty.IsNullable);
                });
        }

        #endregion

        #region Owned types

        [ConditionalFact]
        public virtual void Owned_types_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>(
                        b =>
                        {
                            b.HasKey(e => e.Id).HasName("PK_Custom");

                            b.OwnsOne(
                                eo => eo.EntityWithTwoProperties, eb =>
                                {
                                    eb.HasKey(e => e.AlternateId).HasName("PK_Custom");
                                    eb.WithOwner(e => e.EntityWithOneProperty)
                                      .HasForeignKey(e => e.AlternateId)
                                      .HasConstraintName("FK_Custom");
                                    eb.HasIndex(e => e.Id);

                                    eb.HasOne(e => e.EntityWithStringKey).WithOne();

                                    eb.HasData(
                                        new EntityWithTwoProperties
                                        {
                                            AlternateId = 1,
                                            Id = -1
                                        });
                                });

                            b.HasData(
                                new EntityWithOneProperty
                                {
                                    Id = 1
                                });
                        });

                    builder.Entity<EntityWithStringKey>(
                        b => b.OwnsMany(
                            es => es.Properties, es =>
                            {
                                es.HasKey(e => e.Id);
                                es.HasOne(e => e.EntityWithOneProperty).WithOne();
                            }));
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"")
                        .HasName(""PK_Custom"");

                    b.ToTable(""EntityWithOneProperty"");

                    b.HasData(
                        new
                        {
                            Id = 1
                        });
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringKey"", b =>
                {
                    b.Property<string>(""Id"")
                        .HasColumnType(""nvarchar(450)"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringKey"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.OwnsOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""EntityWithTwoProperties"", b1 =>
                        {
                            b1.Property<int>(""AlternateId"")
                                .ValueGeneratedOnAdd()
                                .HasColumnType(""int"")
                                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                            b1.Property<string>(""EntityWithStringKeyId"")
                                .HasColumnType(""nvarchar(450)"");

                            b1.Property<int>(""Id"")
                                .HasColumnType(""int"");

                            b1.HasKey(""AlternateId"")
                                .HasName(""PK_Custom"");

                            b1.HasIndex(""EntityWithStringKeyId"")
                                .IsUnique()
                                .HasFilter(""[EntityWithTwoProperties_EntityWithStringKeyId] IS NOT NULL"");

                            b1.HasIndex(""Id"");

                            b1.ToTable(""EntityWithOneProperty"");

                            b1.WithOwner(""EntityWithOneProperty"")
                                .HasForeignKey(""AlternateId"")
                                .HasConstraintName(""FK_Custom"");

                            b1.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringKey"", ""EntityWithStringKey"")
                                .WithOne()
                                .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""EntityWithStringKeyId"");

                            b1.HasData(
                                new
                                {
                                    AlternateId = 1,
                                    Id = -1
                                });
                        });
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringKey"", b =>
                {
                    b.OwnsMany(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", ""Properties"", b1 =>
                        {
                            b1.Property<int>(""Id"")
                                .ValueGeneratedOnAdd()
                                .HasColumnType(""int"")
                                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                            b1.Property<int?>(""EntityWithOnePropertyId"")
                                .HasColumnType(""int"");

                            b1.Property<string>(""EntityWithStringKeyId"")
                                .IsRequired()
                                .HasColumnType(""nvarchar(450)"");

                            b1.Property<string>(""Name"")
                                .HasColumnType(""nvarchar(max)"");

                            b1.HasKey(""Id"");

                            b1.HasIndex(""EntityWithOnePropertyId"")
                                .IsUnique()
                                .HasFilter(""[EntityWithOnePropertyId] IS NOT NULL"");

                            b1.HasIndex(""EntityWithStringKeyId"");

                            b1.ToTable(""EntityWithStringProperty"");

                            b1.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""EntityWithOneProperty"")
                                .WithOne()
                                .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", ""EntityWithOnePropertyId"");

                            b1.WithOwner()
                                .HasForeignKey(""EntityWithStringKeyId"");
                        });
                });", usingSystem: true),
                o =>
                {
                    var entityWithOneProperty = o.FindEntityType(typeof(EntityWithOneProperty));
                    Assert.Equal("PK_Custom", entityWithOneProperty.GetKeys().Single().GetName());
                    Assert.Equal(new object[] { 1 }, entityWithOneProperty.GetSeedData().Single().Values);

                    var ownership1 = entityWithOneProperty.FindNavigation(nameof(EntityWithOneProperty.EntityWithTwoProperties))
                        .ForeignKey;
                    Assert.Equal(nameof(EntityWithTwoProperties.AlternateId), ownership1.Properties[0].Name);
                    Assert.Equal(nameof(EntityWithTwoProperties.EntityWithOneProperty), ownership1.DependentToPrincipal.Name);
                    Assert.True(ownership1.IsRequired);
                    Assert.Equal("FK_Custom", ownership1.GetConstraintName());
                    var ownedType1 = ownership1.DeclaringEntityType;
                    Assert.Equal(nameof(EntityWithTwoProperties.AlternateId), ownedType1.FindPrimaryKey().Properties[0].Name);
                    Assert.Equal("PK_Custom", ownedType1.GetKeys().Single().GetName());
                    Assert.Equal(2, ownedType1.GetIndexes().Count());
                    var owned1index1 = ownedType1.GetIndexes().First();
                    Assert.Equal("EntityWithStringKeyId", owned1index1.Properties[0].Name);
                    Assert.True(owned1index1.IsUnique);
                    Assert.Equal("[EntityWithTwoProperties_EntityWithStringKeyId] IS NOT NULL", owned1index1.GetFilter());
                    var owned1index2 = ownedType1.GetIndexes().Last();
                    Assert.Equal("Id", owned1index2.Properties[0].Name);
                    Assert.False(owned1index2.IsUnique);
                    Assert.Null(owned1index2.GetFilter());
                    Assert.Equal(new object[] { 1, -1 }, ownedType1.GetSeedData().Single().Values);
                    Assert.Equal(nameof(EntityWithOneProperty), ownedType1.GetTableName());

                    var entityWithStringKey = o.FindEntityType(typeof(EntityWithStringKey));
                    Assert.Same(
                        entityWithStringKey,
                        ownedType1.FindNavigation(nameof(EntityWithTwoProperties.EntityWithStringKey)).GetTargetType());
                    Assert.Equal(nameof(EntityWithStringKey), entityWithStringKey.GetTableName());

                    var ownership2 = entityWithStringKey.FindNavigation(nameof(EntityWithStringKey.Properties)).ForeignKey;
                    Assert.Equal("EntityWithStringKeyId", ownership2.Properties[0].Name);
                    Assert.Null(ownership2.DependentToPrincipal);
                    Assert.True(ownership2.IsRequired);
                    var ownedType2 = ownership2.DeclaringEntityType;
                    Assert.Equal(nameof(EntityWithStringProperty.Id), ownedType2.FindPrimaryKey().Properties[0].Name);
                    Assert.Equal(1, ownedType2.GetKeys().Count());
                    Assert.Equal(2, ownedType2.GetIndexes().Count());
                    var owned2index1 = ownedType2.GetIndexes().First();
                    Assert.Equal("EntityWithOnePropertyId", owned2index1.Properties[0].Name);
                    Assert.True(owned2index1.IsUnique);
                    Assert.Equal("[EntityWithOnePropertyId] IS NOT NULL", owned2index1.GetFilter());
                    var owned2index2 = ownedType2.GetIndexes().Last();
                    Assert.Equal("EntityWithStringKeyId", owned2index2.Properties[0].Name);
                    Assert.False(owned2index2.IsUnique);
                    Assert.Null(owned2index2.GetFilter());
                    Assert.Equal(nameof(EntityWithStringProperty), ownedType2.GetTableName());

                    Assert.Same(entityWithOneProperty, ownedType2.GetNavigations().Single().GetTargetType());
                });
        }

        [ConditionalFact]
        public virtual void Weak_owned_types_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<Order>().OwnsOne(p => p.OrderBillingDetails, od => od.OwnsOne(c => c.StreetAddress));
                    builder.Entity<Order>().OwnsOne(p => p.OrderShippingDetails, od => od.OwnsOne(c => c.StreetAddress));
                    builder.Entity<Order>().OwnsOne(p => p.OrderInfo, od => od.OwnsOne(c => c.StreetAddress));
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+Order"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""Order"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+Order"", b =>
                {
                    b.OwnsOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+OrderDetails"", ""OrderBillingDetails"", b1 =>
                        {
                            b1.Property<int>(""OrderId"")
                                .ValueGeneratedOnAdd()
                                .HasColumnType(""int"")
                                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                            b1.HasKey(""OrderId"");

                            b1.ToTable(""Order"");

                            b1.WithOwner()
                                .HasForeignKey(""OrderId"");

                            b1.OwnsOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+StreetAddress"", ""StreetAddress"", b2 =>
                                {
                                    b2.Property<int>(""OrderDetailsOrderId"")
                                        .ValueGeneratedOnAdd()
                                        .HasColumnType(""int"")
                                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                                    b2.Property<string>(""City"")
                                        .HasColumnType(""nvarchar(max)"");

                                    b2.HasKey(""OrderDetailsOrderId"");

                                    b2.ToTable(""Order"");

                                    b2.WithOwner()
                                        .HasForeignKey(""OrderDetailsOrderId"");
                                });
                        });

                    b.OwnsOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+OrderDetails"", ""OrderShippingDetails"", b1 =>
                        {
                            b1.Property<int>(""OrderId"")
                                .ValueGeneratedOnAdd()
                                .HasColumnType(""int"")
                                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                            b1.HasKey(""OrderId"");

                            b1.ToTable(""Order"");

                            b1.WithOwner()
                                .HasForeignKey(""OrderId"");

                            b1.OwnsOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+StreetAddress"", ""StreetAddress"", b2 =>
                                {
                                    b2.Property<int>(""OrderDetailsOrderId"")
                                        .ValueGeneratedOnAdd()
                                        .HasColumnType(""int"")
                                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                                    b2.Property<string>(""City"")
                                        .HasColumnType(""nvarchar(max)"");

                                    b2.HasKey(""OrderDetailsOrderId"");

                                    b2.ToTable(""Order"");

                                    b2.WithOwner()
                                        .HasForeignKey(""OrderDetailsOrderId"");
                                });
                        });

                    b.OwnsOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+OrderInfo"", ""OrderInfo"", b1 =>
                        {
                            b1.Property<int>(""OrderId"")
                                .ValueGeneratedOnAdd()
                                .HasColumnType(""int"")
                                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                            b1.HasKey(""OrderId"");

                            b1.ToTable(""Order"");

                            b1.WithOwner()
                                .HasForeignKey(""OrderId"");

                            b1.OwnsOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+StreetAddress"", ""StreetAddress"", b2 =>
                                {
                                    b2.Property<int>(""OrderInfoOrderId"")
                                        .ValueGeneratedOnAdd()
                                        .HasColumnType(""int"")
                                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                                    b2.Property<string>(""City"")
                                        .HasColumnType(""nvarchar(max)"");

                                    b2.HasKey(""OrderInfoOrderId"");

                                    b2.ToTable(""Order"");

                                    b2.WithOwner()
                                        .HasForeignKey(""OrderInfoOrderId"");
                                });
                        });
                });"),
                o =>
                {
                    Assert.Equal(7, o.GetEntityTypes().Count());

                    var order = o.FindEntityType(typeof(Order).FullName);
                    Assert.Equal(1, order.PropertyCount());

                    var orderInfo = order.FindNavigation(nameof(Order.OrderInfo)).GetTargetType();
                    Assert.Equal(1, orderInfo.PropertyCount());

                    var orderInfoAddress = orderInfo.FindNavigation(nameof(OrderInfo.StreetAddress)).GetTargetType();
                    Assert.Equal(2, orderInfoAddress.PropertyCount());

                    var orderBillingDetails = order.FindNavigation(nameof(Order.OrderBillingDetails)).GetTargetType();
                    Assert.Equal(1, orderBillingDetails.PropertyCount());

                    var orderBillingDetailsAddress = orderBillingDetails.FindNavigation(nameof(OrderDetails.StreetAddress)).GetTargetType();
                    Assert.Equal(2, orderBillingDetailsAddress.PropertyCount());

                    var orderShippingDetails = order.FindNavigation(nameof(Order.OrderShippingDetails)).GetTargetType();
                    Assert.Equal(1, orderShippingDetails.PropertyCount());

                    var orderShippingDetailsAddress =
                        orderShippingDetails.FindNavigation(nameof(OrderDetails.StreetAddress)).GetTargetType();
                    Assert.Equal(2, orderShippingDetailsAddress.PropertyCount());
                });
        }

        private class Order
        {
            public int Id { get; set; }
            public OrderDetails OrderBillingDetails { get; set; }
            public OrderDetails OrderShippingDetails { get; set; }
            public OrderInfo OrderInfo { get; set; }
        }

        private class OrderDetails
        {
            public StreetAddress StreetAddress { get; set; }
        }

        private class OrderInfo
        {
            public StreetAddress StreetAddress { get; set; }
        }

        private class StreetAddress
        {
            public string City { get; set; }
        }

        #endregion

        #region Property

        [ConditionalFact]
        public virtual void Property_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>()
                        .Property<int>("Id")
                        .HasAnnotation("AnnotationName", "AnnotationValue")
                        .HasAnnotation(CoreAnnotationNames.TypeMapping, new IntTypeMapping("int"));

                    builder.Ignore<EntityWithTwoProperties>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });"),
                o => Assert.Equal("AnnotationValue", o.GetEntityTypes().First().FindProperty("Id")["AnnotationName"])
            );
        }

        [ConditionalFact]
        public virtual void Custom_value_generator_is_ignored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>().Property<int>("Id").HasValueGenerator<CustomValueGenerator>();
                    builder.Ignore<EntityWithTwoProperties>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });"),
                o => Assert.Null(o.GetEntityTypes().First().FindProperty("Id")[CoreAnnotationNames.ValueGeneratorFactory])
            );
        }

        [ConditionalFact]
        public virtual void Property_isNullable_is_stored_in_snapshot()
        {
            Test(
                builder => builder.Entity<EntityWithStringProperty>().Property<string>("Name").IsRequired(),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(max)"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringProperty"");
                });"),
                o => Assert.False(o.GetEntityTypes().First().FindProperty("Name").IsNullable));
        }

        [ConditionalFact]
        public virtual void Property_ValueGenerated_value_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").HasDefaultValue();
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasDefaultValue(null);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });", usingSystem: true),
                o => Assert.Equal(ValueGenerated.OnAdd, o.GetEntityTypes().First().FindProperty("AlternateId").ValueGenerated));
        }

        [ConditionalFact]
        public virtual void Property_maxLength_is_stored_in_snapshot()
        {
            Test(
                builder => builder.Entity<EntityWithStringProperty>().Property<string>("Name").HasMaxLength(100),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .HasColumnType(""nvarchar(100)"")
                        .HasMaxLength(100);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringProperty"");
                });"),
                o => Assert.Equal(100, o.GetEntityTypes().First().FindProperty("Name").GetMaxLength()));
        }

        [ConditionalFact]
        public virtual void Property_unicodeness_is_stored_in_snapshot()
        {
            Test(
                builder => builder.Entity<EntityWithStringProperty>().Property<string>("Name").IsUnicode(false),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .HasColumnType(""varchar(max)"")
                        .IsUnicode(false);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringProperty"");
                });"),
                o => Assert.False(o.GetEntityTypes().First().FindProperty("Name").IsUnicode()));
        }

        [ConditionalFact]
        public virtual void Property_fixedlengthness_is_stored_in_snapshot()
        {
            Test(
                builder => builder.Entity<EntityWithStringProperty>().Property<string>("Name").IsFixedLength(true),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .HasColumnType(""nvarchar(max)"")
                        .IsFixedLength(true);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringProperty"");
                });"),
                o => Assert.True(o.GetEntityTypes().First().FindProperty("Name").IsFixedLength()));
        }

        [ConditionalFact]
        public virtual void Many_facets_chained_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithStringProperty>()
                        .Property<string>("Name")
                        .HasMaxLength(100)
                        .IsUnicode(false)
                        .HasAnnotation("AnnotationName", "AnnotationValue");
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .HasColumnType(""varchar(100)"")
                        .HasMaxLength(100)
                        .IsUnicode(false)
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringProperty"");
                });"),
                o =>
                {
                    var property = o.GetEntityTypes().First().FindProperty("Name");
                    Assert.Equal(100, property.GetMaxLength());
                    Assert.False(property.IsUnicode());
                    Assert.Equal("AnnotationValue", property["AnnotationName"]);
                });
        }

        [ConditionalFact]
        public virtual void Property_concurrencyToken_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").IsConcurrencyToken();
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .IsConcurrencyToken()
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.True(o.GetEntityTypes().First().FindProperty("AlternateId").IsConcurrencyToken));
        }

        [ConditionalFact]
        public virtual void Property_column_name_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").HasColumnName("CName");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnName(""CName"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal("CName", o.GetEntityTypes().First().FindProperty("AlternateId")["Relational:ColumnName"]));
        }

        [ConditionalFact]
        public virtual void Property_column_type_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").HasColumnType("CType");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""CType"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal("CType", o.GetEntityTypes().First().FindProperty("AlternateId")["Relational:ColumnType"]));
        }

        [ConditionalFact]
        public virtual void Property_default_value_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").HasDefaultValue(1);
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasDefaultValue(1);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal(1, o.GetEntityTypes().First().FindProperty("AlternateId")["Relational:DefaultValue"]));
        }

        [ConditionalFact]
        public virtual void Property_default_value_sql_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").HasDefaultValueSql("SQL");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasDefaultValueSql(""SQL"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal("SQL", o.GetEntityTypes().First().FindProperty("AlternateId")["Relational:DefaultValueSql"]));
        }

        [ConditionalFact]
        public virtual void Property_computed_column_sql_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").HasComputedColumnSql("SQL");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType(""int"")
                        .HasComputedColumnSql(""SQL"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal("SQL", o.GetEntityTypes().First().FindProperty("AlternateId")["Relational:ComputedColumnSql"]));
        }

        [ConditionalFact]
        public virtual void Property_default_value_of_enum_type_is_stored_in_snapshot_without_actual_enum()
        {
            Test(
                builder => builder.Entity<EntityWithEnumType>().Property(e => e.Day).HasDefaultValue(Days.Wed),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithEnumType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<long>(""Day"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""bigint"")
                        .HasDefaultValue(3L);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithEnumType"");
                });"),
                o => Assert.Equal(3L, o.GetEntityTypes().First().FindProperty("Day")["Relational:DefaultValue"]));
        }

        [ConditionalFact]
        public virtual void Property_enum_type_is_stored_in_snapshot_with_custom_conversion_and_seed_data()
        {
            Test(
                builder => builder.Entity<EntityWithEnumType>(
                    eb =>
                    {
                        eb.Property(e => e.Day).HasDefaultValue(Days.Wed)
                            .HasConversion(v => v.ToString(), v => (Days)Enum.Parse(typeof(Days), v));
                        eb.HasData(
                            new
                            {
                                Id = 1,
                                Day = Days.Fri
                            });
                    }),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithEnumType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Day"")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""nvarchar(max)"")
                        .HasDefaultValue(""Wed"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithEnumType"");

                    b.HasData(
                        new
                        {
                            Id = 1,
                            Day = ""Fri""
                        });
                });"),
                o =>
                {
                    var property = o.GetEntityTypes().First().FindProperty("Day");
                    Assert.Equal(typeof(string), property.ClrType);
                    Assert.Equal(nameof(Days.Wed), property["Relational:DefaultValue"]);
                    Assert.False(property.IsNullable);
                });
        }

        [ConditionalFact]
        public virtual void Property_of_nullable_enum()
        {
            Test(
                builder => builder.Entity<EntityWithNullableEnumType>().Property(e => e.Day),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithNullableEnumType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<long?>(""Day"")
                        .HasColumnType(""bigint"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithNullableEnumType"");
                });"),
                o => Assert.True(o.GetEntityTypes().First().FindProperty("Day").IsNullable));
        }

        [ConditionalFact]
        public virtual void Property_of_enum_to_nullable()
        {
            Test(
                builder => builder.Entity<EntityWithEnumType>().Property(e => e.Day)
                    .HasConversion(m => (long?)m, p => p.HasValue ? (Days)p.Value : default),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithEnumType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<long>(""Day"")
                        .HasColumnType(""bigint"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithEnumType"");
                });", usingSystem: true),
                o => Assert.False(o.GetEntityTypes().First().FindProperty("Day").IsNullable));
        }

        [ConditionalFact]
        public virtual void Property_of_nullable_enum_to_string()
        {
            Test(
                builder => builder.Entity<EntityWithNullableEnumType>().Property(e => e.Day).HasConversion<string>(),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithNullableEnumType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Day"")
                        .HasColumnType(""nvarchar(max)"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithNullableEnumType"");
                });"),
                o => Assert.True(o.GetEntityTypes().First().FindProperty("Day").IsNullable));
        }

        [ConditionalFact]
        public virtual void Property_multiple_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().Property<int>("AlternateId").HasColumnName("CName")
                        .HasAnnotation("AnnotationName", "AnnotationValue");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnName(""CName"")
                        .HasColumnType(""int"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    var property = o.GetEntityTypes().First().FindProperty("AlternateId");
                    Assert.Equal(3, property.GetAnnotations().Count());
                    Assert.Equal("AnnotationValue", property["AnnotationName"]);
                    Assert.Equal("CName", property["Relational:ColumnName"]);
                    Assert.Equal("int", property["Relational:ColumnType"]);
                });
        }

        #endregion

        #region HasKey

        [ConditionalFact]
        public virtual void Key_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasAlternateKey(t => t.AlternateId)
                        .HasAnnotation("AnnotationName", "AnnotationValue");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasAlternateKey(""AlternateId"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal(
                    "AnnotationValue", o.GetEntityTypes().First().GetKeys().Where(k => !k.IsPrimaryKey()).First()["AnnotationName"]));
        }

        [ConditionalFact]
        public virtual void Key_name_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasAlternateKey(t => t.AlternateId).HasName("KeyName");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasAlternateKey(""AlternateId"")
                        .HasName(""KeyName"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal(
                    "KeyName", o.GetEntityTypes().First().GetKeys().Where(k => !k.IsPrimaryKey()).First()["Relational:Name"]));
        }

        [ConditionalFact]
        public virtual void Key_multiple_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasAlternateKey(t => t.AlternateId).HasName("IndexName")
                        .HasAnnotation("AnnotationName", "AnnotationValue");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasAlternateKey(""AlternateId"")
                        .HasName(""IndexName"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    var key = o.GetEntityTypes().First().GetKeys().Where(k => !k.IsPrimaryKey()).First();
                    Assert.Equal(2, key.GetAnnotations().Count());
                    Assert.Equal("AnnotationValue", key["AnnotationName"]);
                    Assert.Equal("IndexName", key["Relational:Name"]);
                });
        }

        #endregion

        #region HasIndex

        [ConditionalFact]
        public virtual void Index_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasIndex(t => t.AlternateId)
                        .HasAnnotation("AnnotationName", "AnnotationValue");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal("AnnotationValue", o.GetEntityTypes().First().GetIndexes().First()["AnnotationName"]));
        }

        [ConditionalFact]
        public virtual void Index_isUnique_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasIndex(t => t.AlternateId).IsUnique();
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .IsUnique();

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.True(o.GetEntityTypes().First().GetIndexes().First().IsUnique));
        }

        [ConditionalFact]
        public virtual void Index_name_annotation_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasIndex(t => t.AlternateId).HasName("IndexName");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .HasName(""IndexName"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal("IndexName", o.GetEntityTypes().First().GetIndexes().First()["Relational:Name"]));
        }

        [ConditionalFact]
        public virtual void Index_filter_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasIndex(t => t.AlternateId)
                        .HasFilter("AlternateId <> 0");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .HasFilter(""AlternateId <> 0"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o => Assert.Equal(
                    "AlternateId <> 0",
                    o.GetEntityTypes().First().GetIndexes().First().GetFilter()));
        }

        [ConditionalFact]
        public virtual void Index_multiple_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>().HasIndex(t => t.AlternateId).HasName("IndexName")
                        .HasAnnotation("AnnotationName", "AnnotationValue");
                    builder.Ignore<EntityWithOneProperty>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .HasName(""IndexName"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"");

                    b.ToTable(""EntityWithTwoProperties"");
                });"),
                o =>
                {
                    var index = o.GetEntityTypes().First().GetIndexes().First();
                    Assert.Equal(2, index.GetAnnotations().Count());
                    Assert.Equal("AnnotationValue", index["AnnotationName"]);
                    Assert.Equal("IndexName", index["Relational:Name"]);
                });
        }

        [ConditionalFact]
        public virtual void Index_with_default_constraint_name_exceeding_max()
        {
            Test(
                builder => builder.Entity<EntityWithStringProperty>(
                    x =>
                    {
                        const string propertyName =
                            "SomePropertyWithAnExceedinglyLongIdentifierThatCausesTheDefaultIndexNameToExceedTheMaximumIdentifierLimit";
                        x.Property<string>(propertyName);
                        x.HasIndex(propertyName);
                    }),
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .HasColumnType(""nvarchar(max)"");

                    b.Property<string>(""SomePropertyWithAnExceedinglyLongIdentifierThatCausesTheDefaultIndexNameToExceedTheMaximumIdentifierLimit"")
                        .HasColumnType(""nvarchar(450)"");

                    b.HasKey(""Id"");

                    b.HasIndex(""SomePropertyWithAnExceedinglyLongIdentifierThatCausesTheDefaultIndexNameToExceedTheMaximumIdentifierLimit"");

                    b.ToTable(""EntityWithStringProperty"");
                });"),
                model => Assert.Equal(128, model.GetEntityTypes().First().GetIndexes().First().GetName().Length));
        }

        #endregion

        #region ForeignKey

        [ConditionalFact]
        public virtual void ForeignKey_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>()
                        .HasOne(e => e.EntityWithOneProperty)
                        .WithOne(e => e.EntityWithTwoProperties)
                        .HasForeignKey<EntityWithTwoProperties>(e => e.AlternateId)
                        .HasAnnotation("AnnotationName", "AnnotationValue");
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .IsUnique();

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""EntityWithOneProperty"")
                        .WithOne(""EntityWithTwoProperties"")
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""AlternateId"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o => Assert.Equal(
                    "AnnotationValue", o.FindEntityType(typeof(EntityWithTwoProperties)).GetForeignKeys().First()["AnnotationName"]));
        }

        [ConditionalFact]
        public virtual void ForeignKey_isRequired_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithStringKey>().Ignore(e => e.Properties);
                    builder.Entity<EntityWithStringProperty>()
                        .HasOne<EntityWithStringKey>()
                        .WithOne()
                        .HasForeignKey<EntityWithStringProperty>(e => e.Name)
                        .IsRequired();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringKey"", b =>
                {
                    b.Property<string>(""Id"")
                        .HasColumnType(""nvarchar(450)"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringKey"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(450)"");

                    b.HasKey(""Id"");

                    b.HasIndex(""Name"")
                        .IsUnique();

                    b.ToTable(""EntityWithStringProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringKey"", null)
                        .WithOne()
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", ""Name"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o => Assert.False(o.FindEntityType(typeof(EntityWithStringProperty)).FindProperty("Name").IsNullable));
        }

        [ConditionalFact]
        public virtual void ForeignKey_isUnique_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithStringProperty>()
                        .HasOne<EntityWithStringKey>()
                        .WithMany(e => e.Properties)
                        .HasForeignKey(e => e.Name);
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringKey"", b =>
                {
                    b.Property<string>(""Id"")
                        .HasColumnType(""nvarchar(450)"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithStringKey"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Name"")
                        .HasColumnType(""nvarchar(450)"");

                    b.HasKey(""Id"");

                    b.HasIndex(""Name"");

                    b.ToTable(""EntityWithStringProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringProperty"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithStringKey"", null)
                        .WithMany(""Properties"")
                        .HasForeignKey(""Name"");
                });"),
                o => Assert.False(o.FindEntityType(typeof(EntityWithStringProperty)).GetForeignKeys().First().IsUnique));
        }

        [ConditionalFact]
        public virtual void ForeignKey_deleteBehavior_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>()
                        .HasOne(e => e.EntityWithTwoProperties)
                        .WithMany()
                        .HasForeignKey(e => e.Id);
                    builder.Entity<EntityWithTwoProperties>().Ignore(e => e.EntityWithOneProperty);
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""EntityWithTwoProperties"")
                        .WithMany()
                        .HasForeignKey(""Id"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o => Assert.Equal(
                    DeleteBehavior.Cascade, o.FindEntityType(typeof(EntityWithOneProperty)).GetForeignKeys().First().DeleteBehavior));
        }

        [ConditionalFact]
        public virtual void ForeignKey_deleteBehavior_is_stored_in_snapshot_for_one_to_one()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>()
                        .HasOne(e => e.EntityWithTwoProperties)
                        .WithOne(e => e.EntityWithOneProperty)
                        .HasForeignKey<EntityWithOneProperty>(e => e.Id);
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""EntityWithTwoProperties"")
                        .WithOne(""EntityWithOneProperty"")
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""Id"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o => Assert.Equal(
                    DeleteBehavior.Cascade, o.FindEntityType(typeof(EntityWithOneProperty)).GetForeignKeys().First().DeleteBehavior));
        }

        [ConditionalFact]
        public virtual void ForeignKey_name_preserved_when_generic()
        {
            IModel originalModel = null;

            Test(
                builder =>
                {
                    builder.Entity<EntityWithGenericKey<Guid>>().HasMany<EntityWithGenericProperty<Guid>>().WithOne()
                        .HasForeignKey(e => e.Property);

                    originalModel = builder.Model;
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithGenericKey<System.Guid>"", b =>
                {
                    b.Property<Guid>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""uniqueidentifier"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithGenericKey<Guid>"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithGenericProperty<System.Guid>"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<Guid>(""Property"")
                        .HasColumnType(""uniqueidentifier"");

                    b.HasKey(""Id"");

                    b.HasIndex(""Property"");

                    b.ToTable(""EntityWithGenericProperty<Guid>"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithGenericProperty<System.Guid>"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithGenericKey<System.Guid>"", null)
                        .WithMany()
                        .HasForeignKey(""Property"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });", usingSystem: true),
                model =>
                {
                    var originalParent = originalModel.FindEntityType(typeof(EntityWithGenericKey<Guid>));
                    var parent = model.FindEntityType(originalParent.Name);
                    Assert.NotNull(parent);

                    var originalChild = originalModel.FindEntityType(typeof(EntityWithGenericProperty<Guid>));
                    var child = model.FindEntityType(originalChild.Name);
                    Assert.NotNull(child);

                    var originalForeignKey = originalChild.FindForeignKey(
                        originalChild.FindProperty("Property"),
                        originalParent.FindPrimaryKey(),
                        originalParent);
                    var foreignKey = child.FindForeignKey(
                        child.FindProperty("Property"),
                        parent.FindPrimaryKey(),
                        parent);

                    Assert.Equal(originalForeignKey.GetConstraintName(), foreignKey.GetConstraintName());

                    var originalIndex = originalChild.FindIndex(originalChild.FindProperty("Property"));
                    var index = child.FindIndex(child.FindProperty("Property"));

                    Assert.Equal(originalIndex.GetName(), index.GetName());
                });
        }

        [ConditionalFact]
        public virtual void ForeignKey_constraint_name_is_stored_in_snapshot_as_fluent_api()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>()
                        .HasOne(e => e.EntityWithOneProperty)
                        .WithOne(e => e.EntityWithTwoProperties)
                        .HasForeignKey<EntityWithTwoProperties>(e => e.AlternateId)
                        .HasConstraintName("Constraint");
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .IsUnique();

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""EntityWithOneProperty"")
                        .WithOne(""EntityWithTwoProperties"")
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""AlternateId"")
                        .HasConstraintName(""Constraint"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o => Assert.Equal(
                    "Constraint", o.FindEntityType(typeof(EntityWithTwoProperties)).GetForeignKeys().First()["Relational:Name"]));
        }

        [ConditionalFact]
        public virtual void ForeignKey_multiple_annotations_are_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithTwoProperties>()
                        .HasOne(e => e.EntityWithOneProperty)
                        .WithOne(e => e.EntityWithTwoProperties)
                        .HasForeignKey<EntityWithTwoProperties>(e => e.AlternateId)
                        .HasAnnotation("AnnotationName", "AnnotationValue")
                        .HasConstraintName("Constraint");
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""AlternateId"")
                        .IsUnique();

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""EntityWithOneProperty"")
                        .WithOne(""EntityWithTwoProperties"")
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""AlternateId"")
                        .HasConstraintName(""Constraint"")
                        .HasAnnotation(""AnnotationName"", ""AnnotationValue"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o =>
                {
                    var fk = o.FindEntityType(typeof(EntityWithTwoProperties)).GetForeignKeys().First();
                    Assert.Equal(2, fk.GetAnnotations().Count());
                    Assert.Equal("AnnotationValue", fk["AnnotationName"]);
                    Assert.Equal("Constraint", fk["Relational:Name"]);
                });
        }

        [ConditionalFact]
        public virtual void Do_not_generate_entity_type_builder_again_if_no_foreign_key_is_defined_on_it()
        {
            Test(
                builder =>
                {
                    builder.Entity<BaseType>();
                    builder.Ignore<EntityWithTwoProperties>();
                    builder.Entity<DerivedType>();
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseType"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>(""Discriminator"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(max)"");

                    b.Property<int?>(""NavigationId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasIndex(""NavigationId"");

                    b.ToTable(""BaseType"");

                    b.HasDiscriminator<string>(""Discriminator"").HasValue(""BaseType"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+DerivedType"", b =>
                {
                    b.HasBaseType(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseType"");

                    b.HasDiscriminator().HasValue(""DerivedType"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+BaseType"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""Navigation"")
                        .WithMany()
                        .HasForeignKey(""NavigationId"");
                });", usingSystem: true),
                o => { });
        }

        [ConditionalFact]
        public virtual void ForeignKey_principal_key_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>()
                        .HasOne(e => e.EntityWithTwoProperties)
                        .WithOne(e => e.EntityWithOneProperty)
                        .HasForeignKey<EntityWithOneProperty>(e => e.Id)
                        .HasPrincipalKey<EntityWithTwoProperties>(e => e.AlternateId);
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""EntityWithTwoProperties"")
                        .WithOne(""EntityWithOneProperty"")
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""Id"")
                        .HasPrincipalKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""AlternateId"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o =>
                {
                    Assert.Equal(2, o.FindEntityType(typeof(EntityWithTwoProperties)).GetKeys().Count());
                    Assert.True(o.FindEntityType(typeof(EntityWithTwoProperties)).FindProperty("AlternateId").IsKey());
                });
        }

        [ConditionalFact]
        public virtual void ForeignKey_principal_key_with_non_default_name_is_stored_in_snapshot()
        {
            Test(
                builder =>
                {
                    builder.Entity<EntityWithOneProperty>()
                        .HasOne(e => e.EntityWithTwoProperties)
                        .WithOne(e => e.EntityWithOneProperty)
                        .HasForeignKey<EntityWithOneProperty>(e => e.Id)
                        .HasPrincipalKey<EntityWithTwoProperties>(e => e.AlternateId);

                    builder.Entity<EntityWithTwoProperties>().HasAlternateKey(e => e.AlternateId).HasAnnotation("Name", "Value");
                },
                AddBoilerPlate(
                    GetHeading() + @"
            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.Property<int>(""Id"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithOneProperty"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<int>(""AlternateId"")
                        .HasColumnType(""int"");

                    b.HasKey(""Id"");

                    b.HasAlternateKey(""AlternateId"")
                        .HasAnnotation(""Name"", ""Value"");

                    b.ToTable(""EntityWithTwoProperties"");
                });

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", b =>
                {
                    b.HasOne(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""EntityWithTwoProperties"")
                        .WithOne(""EntityWithOneProperty"")
                        .HasForeignKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithOneProperty"", ""Id"")
                        .HasPrincipalKey(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithTwoProperties"", ""AlternateId"")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });"),
                o =>
                {
                    var entityType = o.FindEntityType(typeof(EntityWithTwoProperties));

                    Assert.Equal(2, entityType.GetKeys().Count());
                    Assert.Equal("Value", entityType.FindKey(entityType.FindProperty("AlternateId"))["Name"]);
                });
        }

        #endregion

        #region SeedData

        [ConditionalFact]
        public virtual void SeedData_annotations_are_stored_in_snapshot()
        {
            static List<IProperty> getAllProperties(IModel model)
                => model
                    .GetEntityTypes()
                    .SelectMany(m => m.GetProperties())
                    .OrderBy(p => p.DeclaringEntityType.Name)
                    .ThenBy(p => p.Name)
                    .ToList();

            var lineString1 = new LineString(
                new[] { new Coordinate(1.1, 2.2), new Coordinate(2.2, 2.2), new Coordinate(2.2, 1.1), new Coordinate(7.1, 7.2) })
            {
                SRID = 4326
            };

            var lineString2 = new LineString(
                new[] { new Coordinate(7.1, 7.2), new Coordinate(20.2, 20.2), new Coordinate(20.20, 1.1), new Coordinate(70.1, 70.2) })
            {
                SRID = 4326
            };

            var multiPoint = new MultiPoint(
                new Point[] { new Point(1.1, 2.2), new Point(2.2, 2.2), new Point(2.2, 1.1) })
            {
                SRID = 4326
            };

            var polygon1 = new Polygon(
                new LinearRing(
                    new[] { new Coordinate(1.1, 2.2), new Coordinate(2.2, 2.2), new Coordinate(2.2, 1.1), new Coordinate(1.1, 2.2) }))
            {
                SRID = 4326
            };

            var polygon2 = new Polygon(
                new LinearRing(
                    new[]
                    {
                        new Coordinate(10.1, 20.2), new Coordinate(20.2, 20.2), new Coordinate(20.2, 10.1), new Coordinate(10.1, 20.2)
                    }))
            {
                SRID = 4326
            };

            var point1 = new Point(1.1, 2.2, 3.3)
            {
                SRID = 4326
            };

            var multiLineString = new MultiLineString(
                new LineString[] { lineString1, lineString2 })
            {
                SRID = 4326
            };

            var multiPolygon = new MultiPolygon(
                new Polygon[] { polygon2, polygon1 })
            {
                SRID = 4326
            };

            var geometryCollection = new GeometryCollection(
                new Geometry[] { lineString1, lineString2, multiPoint, polygon1, polygon2, point1, multiLineString, multiPolygon })
            {
                SRID = 4326
            };

            Test(
                builder =>
                {
                    builder.Entity<EntityWithManyProperties>(
                        eb =>
                        {
                            eb.Property<decimal?>("OptionalProperty");

                            eb.HasData(
                                new EntityWithManyProperties
                                {
                                    Id = 42,
                                    String = "FortyThree",
                                    Bytes = new byte[] { 44, 45 },
                                    Int16 = 46,
                                    Int32 = 47,
                                    Int64 = 48,
                                    Double = 49.0,
                                    Decimal = 50.0m,
                                    DateTime = new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Utc),
                                    DateTimeOffset = new DateTimeOffset(new DateTime(1973, 9, 3, 12, 10, 42, 344), new TimeSpan(1, 0, 0)),
                                    TimeSpan = new TimeSpan(51, 52, 53),
                                    Single = 54.0f,
                                    Boolean = true,
                                    Byte = 55,
                                    UnsignedInt16 = 56,
                                    UnsignedInt32 = 57,
                                    UnsignedInt64 = 58,
                                    Character = '9',
                                    SignedByte = 60,
                                    Enum64 = Enum64.SomeValue,
                                    Enum32 = Enum32.SomeValue,
                                    Enum16 = Enum16.SomeValue,
                                    Enum8 = Enum8.SomeValue,
                                    EnumU64 = EnumU64.SomeValue,
                                    EnumU32 = EnumU32.SomeValue,
                                    EnumU16 = EnumU16.SomeValue,
                                    EnumS8 = EnumS8.SomeValue,
                                    SpatialBGeometryCollection = geometryCollection,
                                    SpatialBLineString = lineString1,
                                    SpatialBMultiLineString = multiLineString,
                                    SpatialBMultiPoint = multiPoint,
                                    SpatialBMultiPolygon = multiPolygon,
                                    SpatialBPoint = point1,
                                    SpatialBPolygon = polygon1,
                                    SpatialCGeometryCollection = geometryCollection,
                                    SpatialCLineString = lineString1,
                                    SpatialCMultiLineString = multiLineString,
                                    SpatialCMultiPoint = multiPoint,
                                    SpatialCMultiPolygon = multiPolygon,
                                    SpatialCPoint = point1,
                                    SpatialCPolygon = polygon1
                                },
                                new
                                {
                                    Id = 43,
                                    String = "FortyThree",
                                    Bytes = new byte[] { 44, 45 },
                                    Int16 = (short)-46,
                                    Int32 = -47,
                                    Int64 = (long)-48,
                                    Double = -49.0,
                                    Decimal = -50.0m,
                                    DateTime = new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Utc),
                                    DateTimeOffset = new DateTimeOffset(new DateTime(1973, 9, 3, 12, 10, 42, 344), new TimeSpan(-1, 0, 0)),
                                    TimeSpan = new TimeSpan(-51, 52, 53),
                                    Single = -54.0f,
                                    Boolean = true,
                                    Byte = (byte)55,
                                    UnsignedInt16 = (ushort)56,
                                    UnsignedInt32 = (uint)57,
                                    UnsignedInt64 = (ulong)58,
                                    Character = '9',
                                    SignedByte = (sbyte)-60,
                                    Enum64 = Enum64.SomeValue,
                                    Enum32 = Enum32.SomeValue,
                                    Enum16 = Enum16.SomeValue,
                                    Enum8 = Enum8.SomeValue,
                                    EnumU64 = EnumU64.SomeValue,
                                    EnumU32 = EnumU32.SomeValue,
                                    EnumU16 = EnumU16.SomeValue,
                                    EnumS8 = EnumS8.SomeValue
                                });
                        });
                    builder.Ignore<EntityWithTwoProperties>();
                },
                @"// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

namespace RootNamespace
{
    [DbContext(typeof(DbContext))]
    partial class Snapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation(""Relational:MaxIdentifierLength"", 128)
                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity(""Microsoft.EntityFrameworkCore.Migrations.ModelSnapshotSqlServerTest+EntityWithManyProperties"", b =>
                {
                    b.Property<int>(""Id"")
                        .ValueGeneratedOnAdd()
                        .HasColumnType(""int"")
                        .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<bool>(""Boolean"")
                        .HasColumnType(""bit"");

                    b.Property<byte>(""Byte"")
                        .HasColumnType(""tinyint"");

                    b.Property<byte[]>(""Bytes"")
                        .HasColumnType(""varbinary(max)"");

                    b.Property<string>(""Character"")
                        .IsRequired()
                        .HasColumnType(""nvarchar(1)"");

                    b.Property<DateTime>(""DateTime"")
                        .HasColumnType(""datetime2"");

                    b.Property<DateTimeOffset>(""DateTimeOffset"")
                        .HasColumnType(""datetimeoffset"");

                    b.Property<decimal>(""Decimal"")
                        .HasColumnType(""decimal(18,2)"");

                    b.Property<double>(""Double"")
                        .HasColumnType(""float"");

                    b.Property<short>(""Enum16"")
                        .HasColumnType(""smallint"");

                    b.Property<int>(""Enum32"")
                        .HasColumnType(""int"");

                    b.Property<long>(""Enum64"")
                        .HasColumnType(""bigint"");

                    b.Property<byte>(""Enum8"")
                        .HasColumnType(""tinyint"");

                    b.Property<short>(""EnumS8"")
                        .HasColumnType(""smallint"");

                    b.Property<int>(""EnumU16"")
                        .HasColumnType(""int"");

                    b.Property<long>(""EnumU32"")
                        .HasColumnType(""bigint"");

                    b.Property<decimal>(""EnumU64"")
                        .HasColumnType(""decimal(20,0)"");

                    b.Property<short>(""Int16"")
                        .HasColumnType(""smallint"");

                    b.Property<int>(""Int32"")
                        .HasColumnType(""int"");

                    b.Property<long>(""Int64"")
                        .HasColumnType(""bigint"");

                    b.Property<decimal?>(""OptionalProperty"")
                        .HasColumnType(""decimal(18,2)"");

                    b.Property<short>(""SignedByte"")
                        .HasColumnType(""smallint"");

                    b.Property<float>(""Single"")
                        .HasColumnType(""real"");

                    b.Property<Geometry>(""SpatialBGeometryCollection"")
                        .HasColumnType(""geography"");

                    b.Property<Geometry>(""SpatialBLineString"")
                        .HasColumnType(""geography"");

                    b.Property<Geometry>(""SpatialBMultiLineString"")
                        .HasColumnType(""geography"");

                    b.Property<Geometry>(""SpatialBMultiPoint"")
                        .HasColumnType(""geography"");

                    b.Property<Geometry>(""SpatialBMultiPolygon"")
                        .HasColumnType(""geography"");

                    b.Property<Geometry>(""SpatialBPoint"")
                        .HasColumnType(""geography"");

                    b.Property<Geometry>(""SpatialBPolygon"")
                        .HasColumnType(""geography"");

                    b.Property<GeometryCollection>(""SpatialCGeometryCollection"")
                        .HasColumnType(""geography"");

                    b.Property<LineString>(""SpatialCLineString"")
                        .HasColumnType(""geography"");

                    b.Property<MultiLineString>(""SpatialCMultiLineString"")
                        .HasColumnType(""geography"");

                    b.Property<MultiPoint>(""SpatialCMultiPoint"")
                        .HasColumnType(""geography"");

                    b.Property<MultiPolygon>(""SpatialCMultiPolygon"")
                        .HasColumnType(""geography"");

                    b.Property<Point>(""SpatialCPoint"")
                        .HasColumnType(""geography"");

                    b.Property<Polygon>(""SpatialCPolygon"")
                        .HasColumnType(""geography"");

                    b.Property<string>(""String"")
                        .HasColumnType(""nvarchar(max)"");

                    b.Property<TimeSpan>(""TimeSpan"")
                        .HasColumnType(""time"");

                    b.Property<int>(""UnsignedInt16"")
                        .HasColumnType(""int"");

                    b.Property<long>(""UnsignedInt32"")
                        .HasColumnType(""bigint"");

                    b.Property<decimal>(""UnsignedInt64"")
                        .HasColumnType(""decimal(20,0)"");

                    b.HasKey(""Id"");

                    b.ToTable(""EntityWithManyProperties"");

                    b.HasData(
                        new
                        {
                            Id = 42,
                            Boolean = true,
                            Byte = (byte)55,
                            Bytes = new byte[] { 44, 45 },
                            Character = ""9"",
                            DateTime = new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Utc),
                            DateTimeOffset = new DateTimeOffset(new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Unspecified), new TimeSpan(0, 1, 0, 0, 0)),
                            Decimal = 50.0m,
                            Double = 49.0,
                            Enum16 = (short)1,
                            Enum32 = 1,
                            Enum64 = 1L,
                            Enum8 = (byte)1,
                            EnumS8 = (short)-128,
                            EnumU16 = 65535,
                            EnumU32 = 4294967295L,
                            EnumU64 = 1234567890123456789m,
                            Int16 = (short)46,
                            Int32 = 47,
                            Int64 = 48L,
                            SignedByte = (short)60,
                            Single = 54f,
                            SpatialBGeometryCollection = (NetTopologySuite.Geometries.GeometryCollection)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;GEOMETRYCOLLECTION Z(LINESTRING Z(1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 7.1 7.2 NaN), LINESTRING Z(7.1 7.2 NaN, 20.2 20.2 NaN, 20.2 1.1 NaN, 70.1 70.2 NaN), MULTIPOINT Z((1.1 2.2 NaN), (2.2 2.2 NaN), (2.2 1.1 NaN)), POLYGON Z((1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 1.1 2.2 NaN)), POLYGON Z((10.1 20.2 NaN, 20.2 20.2 NaN, 20.2 10.1 NaN, 10.1 20.2 NaN)), POINT Z(1.1 2.2 3.3), MULTILINESTRING Z((1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 7.1 7.2 NaN), (7.1 7.2 NaN, 20.2 20.2 NaN, 20.2 1.1 NaN, 70.1 70.2 NaN)), MULTIPOLYGON Z(((10.1 20.2 NaN, 20.2 20.2 NaN, 20.2 10.1 NaN, 10.1 20.2 NaN)), ((1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 1.1 2.2 NaN))))""),
                            SpatialBLineString = (NetTopologySuite.Geometries.LineString)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;LINESTRING (1.1 2.2, 2.2 2.2, 2.2 1.1, 7.1 7.2)""),
                            SpatialBMultiLineString = (NetTopologySuite.Geometries.MultiLineString)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;MULTILINESTRING ((1.1 2.2, 2.2 2.2, 2.2 1.1, 7.1 7.2), (7.1 7.2, 20.2 20.2, 20.2 1.1, 70.1 70.2))""),
                            SpatialBMultiPoint = (NetTopologySuite.Geometries.MultiPoint)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;MULTIPOINT ((1.1 2.2), (2.2 2.2), (2.2 1.1))""),
                            SpatialBMultiPolygon = (NetTopologySuite.Geometries.MultiPolygon)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;MULTIPOLYGON (((10.1 20.2, 20.2 20.2, 20.2 10.1, 10.1 20.2)), ((1.1 2.2, 2.2 2.2, 2.2 1.1, 1.1 2.2)))""),
                            SpatialBPoint = (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;POINT Z(1.1 2.2 3.3)""),
                            SpatialBPolygon = (NetTopologySuite.Geometries.Polygon)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;POLYGON ((1.1 2.2, 2.2 2.2, 2.2 1.1, 1.1 2.2))""),
                            SpatialCGeometryCollection = (NetTopologySuite.Geometries.GeometryCollection)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;GEOMETRYCOLLECTION Z(LINESTRING Z(1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 7.1 7.2 NaN), LINESTRING Z(7.1 7.2 NaN, 20.2 20.2 NaN, 20.2 1.1 NaN, 70.1 70.2 NaN), MULTIPOINT Z((1.1 2.2 NaN), (2.2 2.2 NaN), (2.2 1.1 NaN)), POLYGON Z((1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 1.1 2.2 NaN)), POLYGON Z((10.1 20.2 NaN, 20.2 20.2 NaN, 20.2 10.1 NaN, 10.1 20.2 NaN)), POINT Z(1.1 2.2 3.3), MULTILINESTRING Z((1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 7.1 7.2 NaN), (7.1 7.2 NaN, 20.2 20.2 NaN, 20.2 1.1 NaN, 70.1 70.2 NaN)), MULTIPOLYGON Z(((10.1 20.2 NaN, 20.2 20.2 NaN, 20.2 10.1 NaN, 10.1 20.2 NaN)), ((1.1 2.2 NaN, 2.2 2.2 NaN, 2.2 1.1 NaN, 1.1 2.2 NaN))))""),
                            SpatialCLineString = (NetTopologySuite.Geometries.LineString)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;LINESTRING (1.1 2.2, 2.2 2.2, 2.2 1.1, 7.1 7.2)""),
                            SpatialCMultiLineString = (NetTopologySuite.Geometries.MultiLineString)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;MULTILINESTRING ((1.1 2.2, 2.2 2.2, 2.2 1.1, 7.1 7.2), (7.1 7.2, 20.2 20.2, 20.2 1.1, 70.1 70.2))""),
                            SpatialCMultiPoint = (NetTopologySuite.Geometries.MultiPoint)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;MULTIPOINT ((1.1 2.2), (2.2 2.2), (2.2 1.1))""),
                            SpatialCMultiPolygon = (NetTopologySuite.Geometries.MultiPolygon)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;MULTIPOLYGON (((10.1 20.2, 20.2 20.2, 20.2 10.1, 10.1 20.2)), ((1.1 2.2, 2.2 2.2, 2.2 1.1, 1.1 2.2)))""),
                            SpatialCPoint = (NetTopologySuite.Geometries.Point)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;POINT Z(1.1 2.2 3.3)""),
                            SpatialCPolygon = (NetTopologySuite.Geometries.Polygon)new NetTopologySuite.IO.WKTReader().Read(""SRID=4326;POLYGON ((1.1 2.2, 2.2 2.2, 2.2 1.1, 1.1 2.2))""),
                            String = ""FortyThree"",
                            TimeSpan = new TimeSpan(2, 3, 52, 53, 0),
                            UnsignedInt16 = 56,
                            UnsignedInt32 = 57L,
                            UnsignedInt64 = 58m
                        },
                        new
                        {
                            Id = 43,
                            Boolean = true,
                            Byte = (byte)55,
                            Bytes = new byte[] { 44, 45 },
                            Character = ""9"",
                            DateTime = new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Utc),
                            DateTimeOffset = new DateTimeOffset(new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Unspecified), new TimeSpan(0, -1, 0, 0, 0)),
                            Decimal = -50.0m,
                            Double = -49.0,
                            Enum16 = (short)1,
                            Enum32 = 1,
                            Enum64 = 1L,
                            Enum8 = (byte)1,
                            EnumS8 = (short)-128,
                            EnumU16 = 65535,
                            EnumU32 = 4294967295L,
                            EnumU64 = 1234567890123456789m,
                            Int16 = (short)-46,
                            Int32 = -47,
                            Int64 = -48L,
                            SignedByte = (short)-60,
                            Single = -54f,
                            String = ""FortyThree"",
                            TimeSpan = new TimeSpan(-2, -2, -7, -7, 0),
                            UnsignedInt16 = 56,
                            UnsignedInt32 = 57L,
                            UnsignedInt64 = 58m
                        });
                });
#pragma warning restore 612, 618
        }
    }
}
",
                (snapshotModel, originalModel) =>
                {
                    var originalProperties = getAllProperties(originalModel);
                    var snapshotProperties = getAllProperties(snapshotModel);

                    Assert.Equal(originalProperties.Count,  snapshotProperties.Count);

                    for (var i = 0; i < originalProperties.Count; i++)
                    {
                        var originalProperty = originalProperties[i];
                        var snapshotProperty = snapshotProperties[i];

                        Assert.Equal(originalProperty.DeclaringEntityType.Name, snapshotProperty.DeclaringEntityType.Name);
                        Assert.Equal(originalProperty.Name, snapshotProperty.Name);

                        Assert.Equal(originalProperty.GetColumnType(), snapshotProperty.GetColumnType());
                        Assert.Equal(originalProperty.GetMaxLength(), snapshotProperty.GetMaxLength());
                        Assert.Equal(originalProperty.IsUnicode(), snapshotProperty.IsUnicode());
                        Assert.Equal(originalProperty.IsConcurrencyToken, snapshotProperty.IsConcurrencyToken);
                        Assert.Equal(originalProperty.IsFixedLength(), snapshotProperty.IsFixedLength());
                    }

                    Assert.Collection(
                        snapshotModel.GetEntityTypes().SelectMany(e => e.GetSeedData()),
                        seed =>
                        {
                            Assert.Equal(42, seed["Id"]);
                            Assert.Equal("FortyThree", seed["String"]);
                            Assert.Equal(
                                new byte[]
                                {
                                    44, 45
                                }, seed["Bytes"]);
                            Assert.Equal((short)46, seed["Int16"]);
                            Assert.Equal(47, seed["Int32"]);
                            Assert.Equal((long)48, seed["Int64"]);
                            Assert.Equal(49.0, seed["Double"]);
                            Assert.Equal(50.0m, seed["Decimal"]);
                            Assert.Equal(new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Utc), seed["DateTime"]);
                            Assert.Equal(
                                new DateTimeOffset(new DateTime(1973, 9, 3, 12, 10, 42, 344), new TimeSpan(1, 0, 0)),
                                seed["DateTimeOffset"]);
                            Assert.Equal(new TimeSpan(51, 52, 53), seed["TimeSpan"]);
                            Assert.Equal(54.0f, seed["Single"]);
                            Assert.Equal(true, seed["Boolean"]);
                            Assert.Equal((byte)55, seed["Byte"]);
                            Assert.Equal(56, seed["UnsignedInt16"]);
                            Assert.Equal((long)57, seed["UnsignedInt32"]);
                            Assert.Equal((decimal)58, seed["UnsignedInt64"]);
                            Assert.Equal("9", seed["Character"]);
                            Assert.Equal((short)60, seed["SignedByte"]);
                            Assert.Equal(1L, seed["Enum64"]);
                            Assert.Equal(1, seed["Enum32"]);
                            Assert.Equal((short)1, seed["Enum16"]);
                            Assert.Equal((byte)1, seed["Enum8"]);
                            Assert.Equal(1234567890123456789m, seed["EnumU64"]);
                            Assert.Equal(4294967295L, seed["EnumU32"]);
                            Assert.Equal(65535, seed["EnumU16"]);
                            Assert.Equal((short)-128, seed["EnumS8"]);
                            Assert.Equal(geometryCollection, seed["SpatialBGeometryCollection"]);
                            Assert.Equal(lineString1, seed["SpatialBLineString"]);
                            Assert.Equal(multiLineString, seed["SpatialBMultiLineString"]);
                            Assert.Equal(multiPoint, seed["SpatialBMultiPoint"]);
                            Assert.Equal(multiPolygon, seed["SpatialBMultiPolygon"]);
                            Assert.Equal(point1, seed["SpatialBPoint"]);
                            Assert.Equal(polygon1, seed["SpatialBPolygon"]);
                            Assert.Equal(geometryCollection, seed["SpatialCGeometryCollection"]);
                            Assert.Equal(lineString1, seed["SpatialCLineString"]);
                            Assert.Equal(multiLineString, seed["SpatialCMultiLineString"]);
                            Assert.Equal(multiPoint, seed["SpatialCMultiPoint"]);
                            Assert.Equal(multiPolygon, seed["SpatialCMultiPolygon"]);
                            Assert.Equal(point1, seed["SpatialCPoint"]);
                            Assert.Equal(polygon1, seed["SpatialCPolygon"]);

                            Assert.Equal(4326, ((Geometry)seed["SpatialBGeometryCollection"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialBLineString"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialBMultiLineString"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialBMultiPoint"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialBMultiPolygon"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialBPoint"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialBPolygon"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialCGeometryCollection"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialCLineString"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialCMultiLineString"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialCMultiPoint"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialCMultiPolygon"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialCPoint"]).SRID);
                            Assert.Equal(4326, ((Geometry)seed["SpatialCPolygon"]).SRID);
                        },
                        seed =>
                        {
                            Assert.Equal(43, seed["Id"]);
                            Assert.Equal("FortyThree", seed["String"]);
                            Assert.Equal(
                                new byte[]
                                {
                                    44, 45
                                }, seed["Bytes"]);
                            Assert.Equal((short)-46, seed["Int16"]);
                            Assert.Equal(-47, seed["Int32"]);
                            Assert.Equal((long)-48, seed["Int64"]);
                            Assert.Equal(-49.0, seed["Double"]);
                            Assert.Equal(-50.0m, seed["Decimal"]);
                            Assert.Equal(new DateTime(1973, 9, 3, 12, 10, 42, 344, DateTimeKind.Utc), seed["DateTime"]);
                            Assert.Equal(
                                new DateTimeOffset(new DateTime(1973, 9, 3, 12, 10, 42, 344), new TimeSpan(-1, 0, 0)),
                                seed["DateTimeOffset"]);
                            Assert.Equal(new TimeSpan(-51, 52, 53), seed["TimeSpan"]);
                            Assert.Equal(-54.0f, seed["Single"]);
                            Assert.Equal(true, seed["Boolean"]);
                            Assert.Equal((byte)55, seed["Byte"]);
                            Assert.Equal(56, seed["UnsignedInt16"]);
                            Assert.Equal((long)57, seed["UnsignedInt32"]);
                            Assert.Equal((decimal)58, seed["UnsignedInt64"]);
                            Assert.Equal("9", seed["Character"]);
                            Assert.Equal((short)-60, seed["SignedByte"]);
                            Assert.Equal(1L, seed["Enum64"]);
                            Assert.Equal(1, seed["Enum32"]);
                            Assert.Equal((short)1, seed["Enum16"]);
                            Assert.Equal((byte)1, seed["Enum8"]);
                            Assert.Equal(1234567890123456789m, seed["EnumU64"]);
                            Assert.Equal(4294967295L, seed["EnumU32"]);
                            Assert.Equal(65535, seed["EnumU16"]);
                            Assert.Equal((short)-128, seed["EnumS8"]);
                        });
                });
        }

        #endregion

        protected virtual string GetHeading() => @"
            modelBuilder
                .HasAnnotation(""Relational:MaxIdentifierLength"", 128)
                .HasAnnotation(""SqlServer:ValueGenerationStrategy"", SqlServerValueGenerationStrategy.IdentityColumn);
";

        protected virtual ICollection<BuildReference> GetReferences() => new List<BuildReference>
        {
            BuildReference.ByName("Microsoft.EntityFrameworkCore"),
            BuildReference.ByName("Microsoft.EntityFrameworkCore.Relational"),
            BuildReference.ByName("Microsoft.EntityFrameworkCore.SqlServer"),
            BuildReference.ByName("NetTopologySuite")
        };

        protected virtual string AddBoilerPlate(string code, bool usingSystem = false)
            => $@"// <auto-generated />
{(usingSystem
                ? @"using System;
"
                : "")}using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RootNamespace
{{
    [DbContext(typeof(DbContext))]
    partial class Snapshot : ModelSnapshot
    {{
        protected override void BuildModel(ModelBuilder modelBuilder)
        {{
#pragma warning disable 612, 618{code}
#pragma warning restore 612, 618
        }}
    }}
}}
";

        protected void Test(Action<ModelBuilder> buildModel, string expectedCode, Action<IModel> assert)
            => Test(buildModel, expectedCode, (m, _) => assert(m));

        protected void Test(Action<ModelBuilder> buildModel, string expectedCode, Action<IModel, IModel> assert)
        {
            var modelBuilder = CreateConventionalModelBuilder();
            modelBuilder.HasChangeTrackingStrategy(ChangeTrackingStrategy.Snapshot);
            modelBuilder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion);
            buildModel(modelBuilder);

            var model = modelBuilder.FinalizeModel();

            var sqlServerTypeMappingSource = new SqlServerTypeMappingSource(
                TestServiceFactory.Instance.Create<TypeMappingSourceDependencies>(),
                new RelationalTypeMappingSourceDependencies(
                    new IRelationalTypeMappingSourcePlugin[]
                        { new SqlServerNetTopologySuiteTypeMappingSourcePlugin(NtsGeometryServices.Instance) }));
            var codeHelper = new CSharpHelper(
                sqlServerTypeMappingSource);

            var generator = new CSharpMigrationsGenerator(
                new MigrationsCodeGeneratorDependencies(sqlServerTypeMappingSource),
                new CSharpMigrationsGeneratorDependencies(
                    codeHelper,
                    new CSharpMigrationOperationGenerator(
                        new CSharpMigrationOperationGeneratorDependencies(
                            codeHelper)),
                    new CSharpSnapshotGenerator(
                        new CSharpSnapshotGeneratorDependencies(
                            codeHelper, sqlServerTypeMappingSource))));

            var code = generator.GenerateSnapshot("RootNamespace", typeof(DbContext), "Snapshot", model);

            Assert.Equal(expectedCode, code, ignoreLineEndingDifferences: true);

            var build = new BuildSource
            {
                Sources =
                {
                    code
                }
            };

            foreach (var buildReference in GetReferences())
            {
                build.References.Add(buildReference);
            }

            var assembly = build.BuildInMemory();
            var factoryType = assembly.GetType("RootNamespace.Snapshot");

            var buildModelMethod = factoryType.GetTypeInfo().GetMethod(
                "BuildModel",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(ModelBuilder) },
                null);

            var builder = new ModelBuilder(new ConventionSet());
            builder.Model.RemoveAnnotation(CoreAnnotationNames.ProductVersion);

            buildModelMethod.Invoke(
                Activator.CreateInstance(factoryType),
                new object[] { builder });

            var modelFromSnapshot = new SnapshotModelProcessor(new TestOperationReporter()).Process(builder.Model);

            assert(modelFromSnapshot, model);
        }

        protected ModelBuilder CreateConventionalModelBuilder()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .AddEntityFrameworkSqlServerNetTopologySuite()
                .AddDbContext<DbContext>((p, o) =>
                    o.UseSqlServer("Server=.", b => b.UseNetTopologySuite())
                        .UseInternalServiceProvider(p))
                .BuildServiceProvider();

            using (var serviceScope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<DbContext>())
                {
                    return new ModelBuilder(ConventionSet.CreateConventionSet(context));
                }
            }
        }
    }
}
