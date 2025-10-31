<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='imperial_recruit']"/>
    <xsl:template match="NPCCharacter[@id='imperial_infantryman']"/>
    <xsl:template match="NPCCharacter[@id='imperial_trained_infantryman']"/>
    <xsl:template match="NPCCharacter[@id='imperial_archer']"/>
    <xsl:template match="NPCCharacter[@id='imperial_trained_archer']"/>
    <xsl:template match="NPCCharacter[@id='imperial_heavy_horseman']"/>
    <xsl:template match="NPCCharacter[@id='imperial_cataphract']"/>
    <xsl:template match="NPCCharacter[@id='imperial_elite_cataphract']"/>
    <xsl:template match="NPCCharacter[@id='imperial_veteran_infantryman']"/>
    <xsl:template match="NPCCharacter[@id='imperial_guard_infantry']"/>
    <xsl:template match="NPCCharacter[@id='imperial_legionary']"/>
    <xsl:template match="NPCCharacter[@id='imperial_menavliaton']"/>
    <xsl:template match="NPCCharacter[@id='imperial_elite_menavliaton']"/>
    <xsl:template match="NPCCharacter[@id='imperial_veteran_archer']"/>
    <xsl:template match="NPCCharacter[@id='imperial_sergeant_crossbowman']"/>
    <xsl:template match="NPCCharacter[@id='imperial_palatine_guard']"/>

    <!-- 废弃troop -->
    <xsl:template match="NPCCharacter[@id='imperial_vigla_recruit']"/>
    <xsl:template match="NPCCharacter[@id='imperial_equite']"/>
    <xsl:template match="NPCCharacter[@id='imperial_crossbowman']"/>
    <xsl:template match="NPCCharacter[@id='bucellarii']"/>

</xsl:stylesheet>