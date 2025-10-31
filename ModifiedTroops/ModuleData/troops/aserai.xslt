<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
<xsl:output omit-xml-declaration="yes"/>
<xsl:template match="@*|node()">
    <xsl:copy>
        <xsl:apply-templates select="@*|node()"/>
    </xsl:copy>
</xsl:template>
 
    <xsl:template match="NPCCharacter[@id='aserai_recruit']"/>
    <xsl:template match="NPCCharacter[@id='aserai_tribesman']"/>
    <xsl:template match="NPCCharacter[@id='aserai_footman']"/>
    <xsl:template match="NPCCharacter[@id='aserai_skirmisher']"/>
    <xsl:template match="NPCCharacter[@id='aserai_archer']"/>
    <xsl:template match="NPCCharacter[@id='aserai_master_archer']"/>
    <xsl:template match="NPCCharacter[@id='aserai_infantry']"/>
    <xsl:template match="NPCCharacter[@id='aserai_veteran_infantry']"/>
    <xsl:template match="NPCCharacter[@id='aserai_mameluke_regular']"/>
    <xsl:template match="NPCCharacter[@id='aserai_mameluke_cavalry']"/>
    <xsl:template match="NPCCharacter[@id='aserai_mameluke_heavy_cavalry']"/>
    <xsl:template match="NPCCharacter[@id='aserai_mameluke_axeman']"/>
    <xsl:template match="NPCCharacter[@id='aserai_mameluke_guard']"/>
    <xsl:template match="NPCCharacter[@id='mamluke_palace_guard']"/>

    <!-- 废弃troop -->
    <xsl:template match="NPCCharacter[@id='aserai_mameluke_soldier']"/>
    <xsl:template match="NPCCharacter[@id='aserai_youth']"/>
    <xsl:template match="NPCCharacter[@id='aserai_tribal_horseman']"/>

</xsl:stylesheet>