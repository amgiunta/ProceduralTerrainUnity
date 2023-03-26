namespace VoxelTerrain.ECS
{
    static class ExtendedDeleI11
    {
        // Declare the delegate that takes 12 parameters. T0 is used for the Entity argument
        [Unity.Entities.CodeGeneratedJobForEach.EntitiesForEachCompatible]
        public delegate void CustomForEachDelegate<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>
            (T0 t0, in T1 t1, in T2 t2, in T3 t3, in T4 t4, in T5 t5,
            in T6 t6, in T7 t7, in T8 t8, in T9 t9, in T10 t10, in T11 t11);

        // Declare the function overload
        public static TDescription ForEach<TDescription, T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>
            (this TDescription description, CustomForEachDelegate<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11> codeToRun)
            where TDescription : struct, Unity.Entities.CodeGeneratedJobForEach.ISupportForEachWithUniversalDelegate =>
            LambdaForEachDescriptionConstructionMethods.ThrowCodeGenException<TDescription>();
    }

    static class ExtendedDeleV8I2
    {
        public delegate void CustomForEachDelegate<
            V0, V1, V2, V3, V4, V5, V6, V7,
            I0, I1
        >(
            V0 v0, V1 v1, V2 v2, V3 v3, V4 v4, V5 v5, V6 v6, V7 v7,
            in I0 i0, in I1 i1           
        );

        public static TDescription ForEach<
            TDescription,
            V0, V1, V2, V3, V4, V5, V6, V7,
            I0, I1
        >
        (
            this TDescription description,
            CustomForEachDelegate<
                V0, V1, V2, V3, V4, V5, V6, V7,
                I0, I1
            > codeToRun
        )
        where TDescription : struct, Unity.Entities.CodeGeneratedJobForEach.ISupportForEachWithUniversalDelegate =>
        LambdaForEachDescriptionConstructionMethods.ThrowCodeGenException<TDescription>();
    }

    static class ExtendedDeleV2R6I2
    {
        public delegate void CustomForEachDelegate<
            V0, V1,
            R0, R1, R2, R3, R4, R5,
            I0, I1
        >(
            V0 v0, V1 v1,
            ref R0 r0, ref R1 r1, ref R2 r2, ref R3 r3, ref R4 r4, ref R5 r5,
            in I0 i0, in I1 i1           
        );

        public static TDescription ForEach<
            TDescription,
            V0, V1,
            R0, R1, R2, R3, R4, R5,
            I0, I1
        >
        (
            this TDescription description,
            CustomForEachDelegate<
                V0, V1,
                R0, R1, R2, R3, R4, R5,
                I0, I1
            > codeToRun
        )
        where TDescription : struct, Unity.Entities.CodeGeneratedJobForEach.ISupportForEachWithUniversalDelegate =>
        LambdaForEachDescriptionConstructionMethods.ThrowCodeGenException<TDescription>();
    }

    static class ExtendedDeleV7I2
    {
        public delegate void CustomForEachDelegate<
            V0, V1, V2, V3, V4, V5, V6,
            I0, I1
        >(
            V0 v0, V1 v1, V2 v2, V3 v3, V4 v4, V5 v5, V6 v6,
            in I0 i0, in I1 i1           
        );

        public static TDescription ForEach<
            TDescription,
            V0, V1, V2, V3, V4, V5, V6,
            I0, I1
        >
        (
            this TDescription description,
            CustomForEachDelegate<
                V0, V1, V2, V3, V4, V5, V6,
                I0, I1
            > codeToRun
        )
        where TDescription : struct, Unity.Entities.CodeGeneratedJobForEach.ISupportForEachWithUniversalDelegate =>
        LambdaForEachDescriptionConstructionMethods.ThrowCodeGenException<TDescription>();
    }
}
