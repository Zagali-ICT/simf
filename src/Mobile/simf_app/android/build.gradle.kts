allprojects {
    repositories {
        google()
        mavenCentral()
    }
}

// Force every Android subproject (the Flutter plugins) to compile against API
// 36: this toolchain's flutter.compileSdkVersion is 33, but the plugins' newer
// androidx deps (core 1.18, activity 1.12, browser 1.9) require 36. Registered
// FIRST (before the evaluationDependsOn block below forces evaluation) so the
// hook is in place for every subproject. Reflection avoids the AGP type import.
// (android/ is generated, not committed.)
subprojects {
    afterEvaluate {
        val androidExt = project.extensions.findByName("android")
        if (androidExt != null) {
            try {
                androidExt.javaClass
                    .getMethod("compileSdkVersion", Int::class.javaPrimitiveType)
                    .invoke(androidExt, 36)
            } catch (_: Exception) {
            }
        }
    }
}

val newBuildDir: Directory =
    rootProject.layout.buildDirectory
        .dir("../../build")
        .get()
rootProject.layout.buildDirectory.value(newBuildDir)

subprojects {
    val newSubprojectBuildDir: Directory = newBuildDir.dir(project.name)
    project.layout.buildDirectory.value(newSubprojectBuildDir)
}
subprojects {
    project.evaluationDependsOn(":app")
}

tasks.register<Delete>("clean") {
    delete(rootProject.layout.buildDirectory)
}
